const app = getApp();

function request(url, method = 'GET', data = {}) {
  return new Promise((resolve, reject) => {
    wx.request({
      url: app.globalData.baseUrl + url,
      method,
      data,
      header: { Authorization: 'Bearer ' + app.globalData.token },
      success: (res) => {
        if (res.statusCode === 401) {
          app.logout();
          reject(new Error('登录已过期，请重新登录'));
        } else if (res.statusCode >= 200 && res.statusCode < 300) {
          resolve(res.data);
        } else {
          reject(new Error((res.data && res.data.message) || ('请求失败 ' + res.statusCode)));
        }
      },
      fail: () => reject(new Error('网络异常，请稍后重试'))
    });
  });
}

function toast(msg) {
  wx.showToast({ title: msg, icon: 'none', duration: 2000 });
}

function fen2yuan(f) { return ((f || 0) / 100).toFixed(2); }

module.exports = { request, toast, fen2yuan };
