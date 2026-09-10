const { request, toast } = require('../../utils/request');
const { fenToYuan } = require('../../utils/util');
const app = getApp();

Page({
  data: { tab: 0, mine: [], claimable: [] },

  onShow() { this.load(); },

  async load() {
    try {
      await app.login();
      const [mine, claimable] = await Promise.all([
        request('/api/users/me/coupons?status=' + this.data.tab),
        request('/api/coupons/claimable')
      ]);
      mine.forEach((c) => this.decorate(c));
      claimable.forEach((c) => this.decorate(c));
      this.setData({ mine, claimable });
    } catch (e) { toast(e.message); }
  },

  decorate(c) {
    c.thresholdText = c.thresholdAmount > 0 ? '满' + fenToYuan(c.thresholdAmount) + '可用' : '无门槛';
    c.discountText = fenToYuan(c.discountAmount);
    c.validText = (c.validTo || '').substring(0, 10) + ' 到期';
  },

  onTab(e) {
    this.setData({ tab: Number(e.currentTarget.dataset.tab) }, () => this.load());
  },

  async claim(e) {
    try {
      await request('/api/coupons/' + e.currentTarget.dataset.id + '/claim', 'POST');
      toast('领取成功');
      this.load();
    } catch (err) { toast(err.message); }
  }
});
