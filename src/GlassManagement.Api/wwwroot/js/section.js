// 截面工具辅助 JS — 主要逻辑在 Blazor 端
// 此文件保留用于未来扩展（如 DXF 解析、文件导入等）

window.SectionTool = {
    // 下载截面数据为 JSON
    exportJson: function (data, filename) {
        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename || 'section_data.json';
        a.click();
        URL.revokeObjectURL(url);
    },

    // 从剪贴板粘贴坐标（tab分隔的 X Y 数据）
    parseClipboardPoints: async function () {
        try {
            const text = await navigator.clipboard.readText();
            const lines = text.trim().split('\n');
            const points = [];
            for (const line of lines) {
                const parts = line.trim().split(/[\t,\s]+/);
                if (parts.length >= 2) {
                    const x = parseFloat(parts[0]);
                    const y = parseFloat(parts[1]);
                    if (!isNaN(x) && !isNaN(y)) {
                        points.push({ x, y });
                    }
                }
            }
            return points;
        } catch {
            return [];
        }
    }
};
