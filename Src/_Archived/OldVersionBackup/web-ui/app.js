document.addEventListener('DOMContentLoaded', () => {
    // --- DOM Element Cache ---
    const elements = {
        contractName: document.getElementById('contractName'),
        currentPrice: document.getElementById('currentPrice'),
        marketStatus: document.getElementById('marketStatus'),
        dailyNews: document.getElementById('dailyNews'),
        accountEquity: document.getElementById('accountEquity'),
        tradingAccountBalance: document.getElementById('tradingAccountBalance'),
        usedMargin: document.getElementById('usedMargin'),
        freeMargin: document.getElementById('freeMargin'),
        fundAmountInput: document.getElementById('fundAmount'),
        depositBtn: document.getElementById('depositBtn'),
        withdrawBtn: document.getElementById('withdrawBtn'),
        tradeAmountInput: document.getElementById('tradeAmount'),
        buyBtn: document.getElementById('buyBtn'),
        sellBtn: document.getElementById('sellBtn'),
        positionsTableBody: document.getElementById('positionsTableBody'),
    };

    let socket;
    let currentMarketState = {};

    function formatCurrency(value) {
        return (value || 0).toFixed(2) + 'g';
    }

    // --- WebSocket Connection Handler ---
    function connect() {
        socket = new WebSocket("ws://localhost:8080/");

        socket.onopen = () => console.log("WebSocket connection established.");
        socket.onclose = () => {
            console.log("WebSocket connection closed. Reconnecting in 5 seconds...");
            setTimeout(connect, 5000);
        };
        socket.onerror = (error) => console.error("WebSocket Error:", error);
        socket.onmessage = (event) => {
            try {
                currentMarketState = JSON.parse(event.data);
                updateUI(currentMarketState);
            } catch (e) {
                console.error("Error parsing JSON:", e);
            }
        };
    }

    // --- UI Update Functions ---
    function updateUI(state) {
        // Market Info
        elements.contractName.textContent = state.ContractName;
        elements.currentPrice.textContent = formatCurrency(state.CurrentPrice);
        elements.marketStatus.textContent = state.CurrentStatus;
        elements.marketStatus.className = state.CurrentStatus;
        elements.dailyNews.textContent = state.DailyNews || '无';

        // Account Info
        elements.accountEquity.textContent = formatCurrency(state.AccountEquity);
        elements.tradingAccountBalance.textContent = formatCurrency(state.TradingAccountBalance);
        elements.usedMargin.textContent = formatCurrency(state.UsedMargin);
        elements.freeMargin.textContent = formatCurrency(state.FreeMargin);

        // Positions Table
        updatePositionsTable(state.OpenPositions, state.CurrentPrice);
    }

    function updatePositionsTable(positions, currentPrice) {
        elements.positionsTableBody.innerHTML = '';
        if (!positions || positions.length === 0) {
            const row = `<tr><td colspan="6" style="text-align: center;">当前没有持仓</td></tr>`;
            elements.positionsTableBody.insertAdjacentHTML('beforeend', row);
            return;
        }

        positions.forEach(pos => {
            const pnl = (pos.IsLong ? 1 : -1) * (currentPrice - pos.EntryPrice) * pos.Contracts;
            const pnlClass = pnl >= 0 ? 'profit' : 'loss';
            const direction = pos.IsLong ? '多' : '空';
            const directionClass = pos.IsLong ? 'profit' : 'loss';

            const row = `
                <tr>
                    <td class="${directionClass}">${direction}</td>
                    <td>${pos.Contracts}</td>
                    <td>${formatCurrency(pos.EntryPrice)}</td>
                    <td>${formatCurrency(currentPrice)}</td>
                    <td class="${pnlClass}">${formatCurrency(pnl)}</td>
                    <td>
                        <button class="btn close-btn" data-position-id="${pos.PositionId}">平仓</button>
                    </td>
                </tr>
            `;
            elements.positionsTableBody.insertAdjacentHTML('beforeend', row);
        });
    }

    // --- Command Sender Function ---
    function sendCommand(command) {
        if (socket && socket.readyState === WebSocket.OPEN) {
            socket.send(JSON.stringify(command));
        } else {
            alert("错误：与服务器的连接已断开。");
        }
    }

    // --- Event Listeners ---
    elements.depositBtn.addEventListener('click', () => {
        const amount = parseFloat(elements.fundAmountInput.value);
        if (isNaN(amount) || amount <= 0) {
            alert("请输入有效的存款金额。");
            return;
        }
        sendCommand({ Action: "DEPOSIT", Value: amount });
        elements.fundAmountInput.value = '';
    });

    elements.withdrawBtn.addEventListener('click', () => {
        const amount = parseFloat(elements.fundAmountInput.value);
        if (isNaN(amount) || amount <= 0) {
            alert("请输入有效的取款金额。");
            return;
        }
        sendCommand({ Action: "WITHDRAW", Value: amount });
        elements.fundAmountInput.value = '';
    });

    elements.buyBtn.addEventListener('click', () => {
        const amount = parseInt(elements.tradeAmountInput.value, 10);
        if (isNaN(amount) || amount <= 0) {
            alert("请输入有效的合约数量。");
            return;
        }
        sendCommand({ Action: "OPEN", Amount: amount, IsLong: true });
        elements.tradeAmountInput.value = '';
    });

    elements.sellBtn.addEventListener('click', () => {
        const amount = parseInt(elements.tradeAmountInput.value, 10);
        if (isNaN(amount) || amount <= 0) {
            alert("请输入有效的合约数量。");
            return;
        }
        sendCommand({ Action: "OPEN", Amount: amount, IsLong: false });
        elements.tradeAmountInput.value = '';
    });

    elements.positionsTableBody.addEventListener('click', (event) => {
        if (event.target && event.target.classList.contains('close-btn')) {
            const positionId = event.target.getAttribute('data-position-id');
            if (positionId) {
                sendCommand({ Action: "CLOSE", PositionId: positionId });
            }
        }
    });

    // --- Initial UI State ---
    // Set a default empty state before the first message arrives
    updatePositionsTable([], 0);

    // --- Initial Connection ---
    connect();
});
