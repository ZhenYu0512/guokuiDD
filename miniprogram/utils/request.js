const app = getApp();

// 统一请求封装：自动附带 token，401 时重新登录后重试一次
function request(url, method = 'GET', data = {}, retry = true) {
  return new Promise((resolve, reject) => {
    wx.request({
      url: app.globalData.baseUrl + url,
      method,
      data,
      header: { Authorization: 'Bearer ' + app.globalData.token },
      success: (res) => {
        if (res.statusCode === 401 && retry) {
          app.globalData.token = '';
          app.login().then(() => request(url, method, data, false).then(resolve, reject)).catch(reject);
        } else if (res.statusCode >= 200 && res.statusCode < 300) {
          resolve(res.data);
        } else {
          const msg = (res.data && res.data.message) || ('请求失败 ' + res.statusCode);
          reject(new Error(msg));
        }
      },
      fail: () => reject(new Error('网络异常，请稍后重试'))
    });
  });
}

function toast(msg) {
  wx.showToast({ title: msg, icon: 'none', duration: 2000 });
}

module.exports = { request, toast };
