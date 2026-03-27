window.smhsAuth = {
    login: async function (username, password) {
        try {
            const resp = await fetch('/api/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, password })
            });
            if (!resp.ok) {
                const err = await resp.json().catch(() => ({}));
                return { success: false, error: err.error || '登录失败' };
            }
            const data = await resp.json();
            return {
                success: true,
                token: data.token,
                userJson: JSON.stringify(data.user)
            };
        } catch (e) {
            return { success: false, error: '网络错误：' + e.message };
        }
    },

    tokenLogin: async function (apiToken) {
        try {
            const resp = await fetch('/api/auth/token-login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ apiToken })
            });
            if (!resp.ok) {
                const err = await resp.json().catch(() => ({}));
                return { success: false, error: err.error || '登录失败' };
            }
            const data = await resp.json();
            return {
                success: true,
                token: data.token,
                userJson: JSON.stringify(data.user)
            };
        } catch (e) {
            return { success: false, error: '网络错误：' + e.message };
        }
    },

    getToken: function () {
        return localStorage.getItem('smhs-token');
    },
    getUser: function () {
        const json = localStorage.getItem('smhs-user');
        return json ? JSON.parse(json) : null;
    },
    logout: async function () {
        const token = localStorage.getItem('smhs-token');
        if (token) {
            try {
                await fetch('/api/auth/logout', {
                    method: 'POST',
                    headers: { 'Authorization': 'Bearer ' + token }
                });
            } catch (e) { }
        }
        localStorage.removeItem('smhs-token');
        localStorage.removeItem('smhs-user');
        localStorage.removeItem('smhs-lastPulseId');
        this.stopPulse();
    },
    isLoggedIn: function () {
        return !!localStorage.getItem('smhs-token');
    },

    // ===== Pulse Check =====
    _pulseTimer: null,
    _dotNetRef: null,

    startPulse: function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        this.stopPulse();
        this._schedulePulse();
    },

    stopPulse: function () {
        if (this._pulseTimer) {
            clearTimeout(this._pulseTimer);
            this._pulseTimer = null;
        }
        this._dotNetRef = null;
    },

    _schedulePulse: function () {
        const delay = (Math.floor(Math.random() * 241) + 60) * 1000;
        this._pulseTimer = setTimeout(() => this._doPulse(), delay);
    },

    _doPulse: async function () {
        const token = localStorage.getItem('smhs-token');
        if (!token) {
            this._onPulseFail('登录已过期，请重新登录');
            return;
        }

        const lastPulseId = localStorage.getItem('smhs-lastPulseId') || '';
        try {
            const resp = await fetch('/api/auth/pulse?lastPulseId=' + encodeURIComponent(lastPulseId), {
                headers: { 'Authorization': 'Bearer ' + token }
            });

            if (resp.status === 401) {
                this._onPulseFail('登录已过期，请重新登录');
                return;
            }

            if (!resp.ok) {
                this._schedulePulse();
                return;
            }

            const data = await resp.json();
            if (!data.valid) {
                this._onPulseFail('登录已过期，请重新登录');
                return;
            }

            if (data.pulseId) {
                localStorage.setItem('smhs-lastPulseId', data.pulseId);
            }

            if (data.refreshToken) {
                localStorage.setItem('smhs-token', data.refreshToken);
            }

            if (data.licenseRemainingDays !== null && data.licenseRemainingDays !== undefined) {
                localStorage.setItem('smhs-licenseDays', data.licenseRemainingDays.toString());
            } else {
                localStorage.removeItem('smhs-licenseDays');
            }

            this._schedulePulse();
        } catch (e) {
            this._schedulePulse();
        }
    },

    _onPulseFail: function (msg) {
        localStorage.removeItem('smhs-token');
        localStorage.removeItem('smhs-user');
        localStorage.removeItem('smhs-lastPulseId');
        if (this._dotNetRef) {
            try { this._dotNetRef.invokeMethodAsync('OnPulseExpired', msg); } catch (e) { }
        }
        alert(msg);
        window.location.href = '/login';
    }
};
