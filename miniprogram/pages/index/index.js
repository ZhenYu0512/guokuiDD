const { request } = require('../../utils/request');
const { fenToYuan } = require('../../utils/util');
const app = getApp();

Page({
  data: { store: null, banners: [], featured: [], notice: '' },
  onShow() { this.load(); },
  load() {
    request('/api/home?storeId=' + app.globalData.storeId).then((res) => {
      res.featured.forEach((p) => (p.priceText = fenToYuan(p.basePrice)));
      this.setData({ store: res.store, banners: res.banners, featured: res.featured, notice: res.store.notice || '' });
    }).catch((e) => wx.showToast({ title: e.message, icon: 'none' }));
  },
  goMenu() { wx.switchTab({ url: '/pages/menu/menu' }); }
});
