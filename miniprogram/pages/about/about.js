Page({
  data: {
    company: '武汉市云与慧网络科技有限公司',
    cloud: '火山引擎',
    icpMain: '鄂ICP备2026013201号',
    icpDomain: '鄂ICP备2026013201号-1',
    icpMini: '鄂ICP备2026013201号-3X'
  },
  copyIcp(e) {
    wx.setClipboardData({ data: e.currentTarget.dataset.text });
  }
});
