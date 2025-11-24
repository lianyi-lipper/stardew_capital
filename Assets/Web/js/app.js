// ========== Main Application Logic ==========

// ========== 启动应用 ==========
window.onload = function () {
    initChart();

    // 初始加载
    updateMarketData();
    updateAccount();
    updateNews();
    updateOrderBook();
    updatePositions();

    // 定时刷新
    setInterval(updateMarketData, 1000);
    setInterval(updateAccount, 2000);
    setInterval(updateNews, 5000);
    setInterval(updateOrderBook, 1000);
    setInterval(updatePositions, 2000);
};
