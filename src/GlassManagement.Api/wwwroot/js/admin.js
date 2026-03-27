window.smhsAdmin = {
    _fetch: async function (token, url, method, body) {
        try {
            const opts = {
                method: method || 'GET',
                headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' }
            };
            if (body) opts.body = JSON.stringify(body);
            const resp = await fetch(url, opts);
            if (!resp.ok) {
                const err = await resp.json().catch(() => ({}));
                return { success: false, error: err.error || '操作失败' };
            }
            const data = await resp.json();
            return { success: true, ...data };
        } catch (e) {
            return { success: false, error: '网络错误：' + e.message };
        }
    },

    getUsers: async function (token, page, pageSize) {
        const resp = await fetch('/api/admin/users?page=' + page + '&pageSize=' + pageSize, {
            headers: { 'Authorization': 'Bearer ' + token }
        });
        if (!resp.ok) return { items: [], total: 0, page: 1 };
        return await resp.json();
    },

    createUser: async function (token, username, password, displayName, email, phone, company, role, licenseExpiry, maxProjects) {
        return await this._fetch(token, '/api/admin/users', 'POST', {
            username, password, displayName: displayName || undefined,
            email: email || undefined, phone: phone || undefined,
            company: company || undefined, role,
            licenseExpiry: licenseExpiry || undefined,
            maxProjects: maxProjects || 10
        });
    },

    updateUser: async function (token, id, displayName, email, phone, company, notes, maxProjects) {
        return await this._fetch(token, '/api/admin/users/' + id, 'PUT', {
            displayName, email, phone, company, notes, maxProjects
        });
    },

    setLicense: async function (token, id, licenseExpiry) {
        return await this._fetch(token, '/api/admin/users/' + id + '/license', 'PUT', {
            licenseExpiry: licenseExpiry || null
        });
    },

    toggleUser: async function (token, id) {
        return await this._fetch(token, '/api/admin/users/' + id + '/toggle', 'PUT', {});
    },

    deleteUser: async function (token, id) {
        return await this._fetch(token, '/api/admin/users/' + id, 'DELETE');
    },

    regenerateToken: async function (token, userId) {
        return await this._fetch(token, '/api/admin/users/' + userId + '/regenerate-token', 'PUT', {});
    },

    getUserUsage: async function (token, userId, from, to) {
        let url = '/api/admin/users/' + userId + '/usage';
        const params = [];
        if (from) params.push('from=' + encodeURIComponent(from));
        if (to) params.push('to=' + encodeURIComponent(to));
        if (params.length) url += '?' + params.join('&');
        const resp = await fetch(url, { headers: { 'Authorization': 'Bearer ' + token } });
        if (!resp.ok) return null;
        return await resp.json();
    },

    getUsageSummary: async function (token, from, to) {
        let url = '/api/admin/usage/summary';
        const params = [];
        if (from) params.push('from=' + encodeURIComponent(from));
        if (to) params.push('to=' + encodeURIComponent(to));
        if (params.length) url += '?' + params.join('&');
        const resp = await fetch(url, { headers: { 'Authorization': 'Bearer ' + token } });
        if (!resp.ok) return null;
        return await resp.json();
    }
};
