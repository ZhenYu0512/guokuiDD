App({
  globalData: {
    // 开发环境使用本地后端；上线前改为 https 域名并配置 request 合法域名
    baseUrl: 'https://localhost:5000',
    storeId: 1,
    token: '',
    userInfo: null
  },
  onLaunch() {
    this.globalData.token = wx.getStorageSync('token') || '';
  },
  // 静默登录：开发模式下 code 直接作为登录凭证
  login() {
    return new Promise((resolve, reject) => {
      if (this.globalData.token) return resolve(this.globalData.token);
      wx.login({
        success: (res) => {
          wx.request({
            url: this.globalData.baseUrl + '/api/auth/wechat-login',
            method: 'POST',
            data: { code: res.code, nickName: '微信用户' },
            success: (r) => {
              if (r.statusCode === 200 && r.data.token) {
                this.globalData.token = r.data.token;
                wx.setStorageSync('token', r.data.token);
                resolve(r.data.token);
              } else {
                reject(new Error((r.data && r.data.message) || '登录失败'));
              }
            },
            fail: reject
          });
        },
        fail: reject
      });
    });
  }
});
