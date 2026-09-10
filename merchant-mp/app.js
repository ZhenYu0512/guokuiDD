App({
  globalData: {
    // 与用户端小程序共用同一后端
    baseUrl: 'https://guokui.api.yunyuhui.cn',
    token: '',
    username: ''
  },
  onLaunch() {
    this.globalData.token = wx.getStorageSync('merchant_token') || '';
    this.globalData.username = wx.getStorageSync('merchant_username') || '';
  },
  isLoggedIn() {
    return !!this.globalData.token;
  },
  logout() {
    this.globalData.token = '';
    this.globalData.username = '';
    wx.removeStorageSync('merchant_token');
    wx.removeStorageSync('merchant_username');
    wx.reLaunch({ url: '/pages/login/login' });
  }
});
