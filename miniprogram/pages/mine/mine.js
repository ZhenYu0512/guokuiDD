const { request, toast } = require('../../utils/request');
const app = getApp();

Page({
  data: { me: null, pointsRecords: [], showPoints: false },

  onShow() { this.load(); },

  async load() {
    try {
      await app.login();
      const me = await request('/api/users/me');
      this.setData({ me });
    } catch (e) { toast(e.message); }
  },

  async openCard() {
    try {
      await request('/api/users/me/member-card', 'POST');
      toast('开卡成功');
      this.load();
    } catch (e) { toast(e.message); }
  },

  async togglePoints() {
    const show = !this.data.showPoints;
    this.setData({ showPoints: show });
    if (show && !this.data.pointsRecords.length) {
      try {
        const records = await request('/api/users/me/points-records');
        records.forEach((r) => (r.timeText = (r.createdAt || '').replace('T', ' ').substring(0, 16)));
        this.setData({ pointsRecords: records });
      } catch (e) { toast(e.message); }
    }
  },

  goCoupons() { wx.navigateTo({ url: '/pages/coupons/coupons' }); },

  goAbout() { wx.navigateTo({ url: '/pages/about/about' }); }
});
