const app = getApp();
const { toast } = require('../../utils/request');

Page({
  data: { username: '', password: '', loading: false },

  onLoad() {
    if (app.isLoggedIn()) wx.switchTab({ url: '/pages/orders/orders' });
  },

  onUserInput(e) { this.setData({ username: e.detail.value }); },
  onPwdInput(e) { this.setData({ password: e.detail.value }); },

  login() {
    if (!this.data.username.trim() || !this.data.password) return toast('请输入账号和密码');
    this.setData({ loading: true });
    wx.request({
      url: app.globalData.baseUrl + '/api/admin/auth/login',
      method: 'POST',
      data: { username: this.data.username.trim(), password: this.data.password },
      success: (res) => {
        this.setData({ loading: false });
        if (res.statusCode === 200 && res.data.token) {
          app.globalData.token = res.data.token;
          app.globalData.username = res.data.username;
          wx.setStorageSync('merchant_token', res.data.token);
          wx.setStorageSync('merchant_username', res.data.username);
          wx.switchTab({ url: '/pages/orders/orders' });
        } else {
          toast((res.data && res.data.message) || '登录失败');
        }
      },
      fail: () => {
        this.setData({ loading: false });
        toast('网络异常');
      }
    });
  }
});
