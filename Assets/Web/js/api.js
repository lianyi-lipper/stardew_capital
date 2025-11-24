// ========== API Calls Module ==========

// 从URL获取symbol参数，如果没有则使用默认值
const urlParams = new URLSearchParams(window.location.search);
let currentSymbol = urlParams.get('symbol') || 'PARSNIP-SPR-28';
let currentPrice = 0;
let showNewsHistory = false;

// ========== 更新市场数据 ==========
async function updateMarketData() {
    try {
        const response = await fetch('/api/ticker');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const data = await response.json();
        if (data && data.length > 0) {
            const item = data[0];
            currentPrice = item.price;

            document.getElementById('price-display').textContent = item.price.toFixed(2) + ' g';
            document.getElementById('symbol-display').textContent = item.symbol;

            // 新增字段显示
            document.getElementById('spot-price').textContent = item.spotPrice.toFixed(2);
            const basis = item.basis;
            const basisEl = document.getElementById('basis-price');
            basisEl.textContent = (basis >= 0 ? '+' : '') + basis.toFixed(2);
            basisEl.style.color = basis >= 0 ? '#4caf50' : '#f44336';

            document.getElementById('status').textContent = 'LIVE';
            document.getElementById('status').className = 'status live';

            updateChartPrice(item.price);
        }
    } catch (e) {
        document.getElementById('status').textContent = 'OFFLINE';
        document.getElementById('status').className = 'status offline';
        console.error("Market data fetch error:", e);
    }
}

// ========== 更新账户风险 ==========
async function updateAccount() {
    try {
        const response = await fetch('/api/account');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const data = await response.json();

        document.getElementById('equity-val').textContent = data.equity.toFixed(0) + 'g';

        const levelEl = document.getElementById('margin-level');
        const level = data.marginLevel;

        if (level > 100) { // > 10000%
            levelEl.textContent = 'Safe';
            levelEl.className = 'risk-value risk-safe';
        } else {
            levelEl.textContent = (level * 100).toFixed(1) + '%';
            if (level < 0.5) levelEl.className = 'risk-value risk-danger';
            else if (level < 0.8) levelEl.className = 'risk-value risk-warning';
            else levelEl.className = 'risk-value risk-safe';
        }

    } catch (e) {
        console.error("Account fetch error:", e);
    }
}

// ========== 更新新闻 ==========
async function updateNews() {
    if (showNewsHistory) return; // 如果正在查看历史，不自动更新

    try {
        const response = await fetch('/api/news');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const newsList = await response.json();
        renderNews(newsList, false);

    } catch (e) {
        console.error("News fetch error:", e);
    }
}

// ========== 切换新闻历史 ==========
function toggleNewsHistory() {
    showNewsHistory = !showNewsHistory;
    const btn = document.getElementById('btn-news-history');

    if (showNewsHistory) {
        btn.style.background = '#4caf50';
        btn.style.color = 'white';
        btn.textContent = 'Active';
        fetchNewsHistory();
    } else {
        btn.style.background = 'none';
        btn.style.color = '#4caf50';
        btn.textContent = 'History';
        updateNews(); // 切换回 Active
    }
}

// ========== 获取新闻历史 ==========
async function fetchNewsHistory() {
    try {
        const response = await fetch('/api/news/history');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const newsList = await response.json();
        renderNews(newsList, true);
    } catch (e) {
        console.error("News history fetch error:", e);
    }
}

// ========== 渲染新闻列表 ==========
function renderNews(newsList, isHistory) {
    const container = document.getElementById('news-list');

    if (!newsList || newsList.length === 0) {
        container.innerHTML = '<div style="padding:10px; text-align:center; color:#666;">无记录</div>';
        return;
    }

    // 如果是历史记录，倒序显示
    if (isHistory) {
        newsList.reverse();
    }

    let html = '';
    newsList.forEach(news => {
        const dayStr = news.day ? `Day ${news.day}` : '';
        html += `<div class="news-item">
            <div class="news-headline">
                ${news.headline} 
                ${isHistory ? `<span style="font-size:10px; color:#888; float:right;">${dayStr}</span>` : ''}
            </div>
            <div class="news-desc">${news.description}</div>
        </div>`;
    });
    container.innerHTML = html;
}

// ========== 更新订单簿 ==========
async function updateOrderBook() {
    try {
        const response = await fetch('/api/orderbook?symbol=' + encodeURIComponent(currentSymbol));
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const data = await response.json();
        renderOrderBook(data);
    } catch (e) {
        console.error("OrderBook fetch error:", e);
        document.getElementById('orderbook').innerHTML = '<div class="loading">订单簿加载失败</div>';
    }
}

// ========== 渲染订单簿 ==========
function renderOrderBook(data) {
    const container = document.getElementById('orderbook');
    if (!data || (!data.asks && !data.bids)) {
        container.innerHTML = '<div class="loading">暂无数据</div>';
        return;
    }

    let html = '';

    // 卖盘 (倒序显示，最低价在下)
    const asks = (data.asks || []).slice(0, 5).reverse();
    asks.forEach(order => {
        const isPlayer = order.isPlayerOrder || false;
        const playerClass = isPlayer ? ' orderbook-player' : '';
        html += `<div class="orderbook-row ask${playerClass}">
            <span>${order.price.toFixed(2)}</span>
            <span>x${order.quantity}${isPlayer ? ' 👤' : ''}</span>
        </div>`;
    });

    // 中间价
    if (data.midPrice) {
        html += `<div class="orderbook-row mid">${data.midPrice.toFixed(2)} g (MID)</div>`;
    }

    // 买盘 (正序显示，最高价在上)
    const bids = (data.bids || []).slice(0, 5);
    bids.forEach(order => {
        const isPlayer = order.isPlayerOrder || false;
        const playerClass = isPlayer ? ' orderbook-player' : '';
        html += `<div class="orderbook-row bid${playerClass}">
            <span>${order.price.toFixed(2)}</span>
            <span>x${order.quantity}${isPlayer ? ' 👤' : ''}</span>
        </div>`;
    });

    container.innerHTML = html;
}

// ========== 更新持仓 ==========
async function updatePositions() {
    try {
        const response = await fetch('/api/positions');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const positions = await response.json();
        renderPositions(positions);
    } catch (e) {
        console.error("Positions fetch error:", e);
    }
}

// ========== 渲染持仓 ==========
function renderPositions(positions) {
    const container = document.getElementById('positions');
    if (!positions || positions.length === 0) {
        container.innerHTML = '<div class="loading">暂无持仓</div>';
        return;
    }

    let html = '';
    positions.forEach(pos => {
        const pnl = pos.unrealizedPnL || 0;
        const pnlClass = pnl >= 0 ? 'profit-positive' : 'profit-negative';
        html += `<div class="position-item">
            <span>${pos.quantity > 0 ? '🟢 LONG' : '🔴 SHORT'} ${Math.abs(pos.quantity)}</span>
            <span class="${pnlClass}">${pnl >= 0 ? '+' : ''}${pnl.toFixed(2)}g</span>
        </div>`;
    });
    container.innerHTML = html;
}

// ========== 下市价单 ==========
async function placeMarketOrder(isBuy) {
    const quantity = parseInt(document.getElementById('market-quantity').value);
    const leverage = parseInt(document.getElementById('market-leverage').value);

    const orderQuantity = isBuy ? quantity : -quantity;

    try {
        const response = await fetch('/api/order/market', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ symbol: currentSymbol, quantity: orderQuantity, leverage })
        });

        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        alert(`${isBuy ? '买入' : '卖出'}成功! 数量: ${quantity}, 杠杆: ${leverage}x`);
        updatePositions();
    } catch (e) {
        alert(`下单失败: ${e.message}`);
    }
}

// ========== 下限价单 ==========
async function placeLimitOrder(isBuy) {
    const price = parseFloat(document.getElementById('limit-price').value);
    const quantity = parseInt(document.getElementById('limit-quantity').value);
    const leverage = parseInt(document.getElementById('limit-leverage').value);

    try {
        const response = await fetch('/api/order/limit', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                symbol: currentSymbol,
                isBuy,
                price,
                quantity,
                leverage
            })
        });

        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const result = await response.json();
        alert(`限价单已提交! OrderID: ${result.orderId}`);
        updateOrderBook();
    } catch (e) {
        alert(`下单失败: ${e.message}`);
    }
}

// ========== 平仓所有 ==========
async function closeAllPositions() {
    if (!confirm('确定要平仓所有持仓吗?')) return;

    try {
        const response = await fetch('/api/positions/closeall', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ symbol: currentSymbol })
        });

        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        alert('已平仓所有持仓!');
        updatePositions();
    } catch (e) {
        alert(`平仓失败: ${e.message}`);
    }
}
