// ========== Market List Page Logic ==========

let futuresData = [];
let updateInterval = null;

// ========== 获取所有期货数据 ==========
async function fetchFuturesData() {
    try {
        const response = await fetch('/api/ticker');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const data = await response.json();
        futuresData = data || [];

        updateStatusIndicator(true);
        renderFuturesGrid();
    } catch (e) {
        console.error("Futures data fetch error:", e);
        updateStatusIndicator(false);
        document.getElementById('futures-grid').innerHTML =
            '<div class="loading-overlay" style="color: #f44336;">连接失败，请检查服务器</div>';
    }
}

// ========== 更新状态指示器 ==========
function updateStatusIndicator(isLive) {
    const dot = document.getElementById('status-dot');
    const text = document.getElementById('status-text');

    if (isLive) {
        dot.className = 'status-indicator status-live';
        text.textContent = 'LIVE';
        text.style.color = '#4caf50';
    } else {
        dot.className = 'status-indicator status-offline';
        text.textContent = 'OFFLINE';
        text.style.color = '#f44336';
    }
}

// ========== 渲染期货卡片列表 ==========
function renderFuturesGrid() {
    const grid = document.getElementById('futures-grid');

    if (!futuresData || futuresData.length === 0) {
        grid.innerHTML = '<div class="loading-overlay">暂无数据</div>';
        return;
    }

    let html = '';
    futuresData.forEach(futures => {
        const change = futures.change || 0;
        const changePercent = futures.changePercent || 0;
        const isPositive = change >= 0;
        const changeClass = isPositive ? 'change-positive' : 'change-negative';
        const changeSymbol = isPositive ? '+' : '';

        html += `
            <div class="futures-card" onclick="navigateToDetail('${futures.symbol}')">
                <div class="card-symbol">${futures.symbol}</div>
                <div class="card-price">${futures.price.toFixed(2)} g</div>
                
                <div class="card-change">
                    <span class="change-amount ${changeClass}">
                        ${changeSymbol}${change.toFixed(2)} g
                    </span>
                    <span class="change-amount ${changeClass}">
                        ${changeSymbol}${changePercent.toFixed(2)}%
                    </span>
                </div>

                <div class="card-info">
                    现货价: ${futures.spotPrice.toFixed(2)} g | 
                    基差: ${futures.basis >= 0 ? '+' : ''}${futures.basis.toFixed(2)} g
                </div>
            </div>
        `;
    });

    grid.innerHTML = html;
}

// ========== 导航到详情页 ==========
function navigateToDetail(symbol) {
    window.location.href = `index.html?symbol=${encodeURIComponent(symbol)}`;
}

// ========== 初始化和轮询 ==========
window.onload = function () {
    // 初始加载
    fetchFuturesData();

    // 定时刷新（每2秒）
    updateInterval = setInterval(fetchFuturesData, 2000);
};

// ========== 页面卸载时清理 ==========
window.onbeforeunload = function () {
    if (updateInterval) {
        clearInterval(updateInterval);
    }
};
