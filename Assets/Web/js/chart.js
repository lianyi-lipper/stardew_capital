// ========== Chart Management ==========
let chart = null;
let lineSeries = null;
let impactSeries = null;
let showImpact = false;

// ========== 初始化图表 ==========
function initChart() {
    if (typeof LightweightCharts === 'undefined') {
        console.error("LightweightCharts library not loaded!");
        return;
    }

    const chartContainer = document.getElementById('chart-container');
    chart = LightweightCharts.createChart(chartContainer, {
        layout: { background: { type: 'solid', color: 'transparent' }, textColor: '#d1d4dc' },
        grid: { vertLines: { color: 'rgba(255,255,255,0.1)' }, horzLines: { color: 'rgba(255,255,255,0.1)' } },
        rightPriceScale: { borderColor: 'rgba(255,255,255,0.2)' },
        timeScale: { borderColor: 'rgba(255,255,255,0.2)', timeVisible: true, secondsVisible: false },
    });

    lineSeries = chart.addSeries(LightweightCharts.LineSeries, {
        color: '#4caf50',
        lineWidth: 2,
        title: 'Price'
    });

    // 初始化 Impact Series (默认隐藏)
    impactSeries = chart.addSeries(LightweightCharts.LineSeries, {
        color: '#ff9800',
        lineWidth: 2,
        title: 'Impact',
        priceScaleId: 'left' // 使用左侧坐标轴
    });
    chart.priceScale('left').applyOptions({
        visible: false, // 初始隐藏
        borderColor: 'rgba(255,255,255,0.2)'
    });
    impactSeries.applyOptions({ visible: false });

    window.addEventListener('resize', () => {
        chart.resize(chartContainer.clientWidth, chartContainer.clientHeight);
    });
}

// ========== 切换 Impact 显示 ==========
async function toggleImpact() {
    showImpact = document.getElementById('show-impact').checked;

    if (impactSeries) {
        impactSeries.applyOptions({ visible: showImpact });
        chart.priceScale('left').applyOptions({ visible: showImpact });

        if (showImpact) {
            await fetchImpactHistory();
        }
    }
}

// ========== 获取 Impact 历史 ==========
async function fetchImpactHistory() {
    try {
        const response = await fetch('/api/impact/history?symbol=' + encodeURIComponent(currentSymbol));
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const data = await response.json();
        if (data && data.length > 0) {
            // 假设数据是简单的数值数组，我们需要构造时间序列
            // 这里简化处理，假设每秒一个点，倒推时间
            const now = Math.floor(Date.now() / 1000);
            const chartData = data.map((val, index) => ({
                time: now - (data.length - 1 - index), // 简单倒推
                value: val
            }));
            impactSeries.setData(chartData);
        }
    } catch (e) {
        console.error("Impact history fetch error:", e);
    }
}

// ========== 更新图表价格 ==========
function updateChartPrice(price) {
    const now = Math.floor(Date.now() / 1000);
    if (lineSeries) {
        lineSeries.update({ time: now, value: price });
    }
}
