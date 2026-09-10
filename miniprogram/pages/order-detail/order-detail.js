const { request, toast } = require('../../utils/request');
const { fenToYuan, statusText, fmtTime } = require('../../utils/util');
const app = getApp();

Page({
  data: { order: null },

  onLoad(options) { this.id = options.id; },
  onShow() { this.load(); },

  async load() {
    try {
      await app.login();
      const o = await request('/api/orders/' + this.id);
      o.payText = fenToYuan(o.payAmount);
      o.totalText = fenToYuan(o.totalAmount);
      o.couponText = fenToYuan(o.couponDiscount);
      o.pointsText = fenToYuan(o.pointsDiscount);
      o.statusText = statusText(o.status);
      o.timeText = fmtTime(o.createdAt);
      o.items.forEach((i) => {
        i.subtotalText = fenToYuan(i.subtotal);
        i.specText = (i.specs || []).map((s) => s.option).join('/');
        i.addonText = (i.addons || []).map((a) => a.name).join(' + ');
      });
      this.setData({ order: o });
    } catch (e) { toast(e.message); }
  },

  async pay() {
    try {
      const res = await request('/api/orders/' + this.id + '/pay', 'POST');
      if (res.mode === 'mock' || res.paid) {
        toast('支付成功');
        return this.load();
      }
      wx.requestPayment({
        ...res.payParams,
        success: async () => {
          try { await request('/api/orders/' + this.id + '/sync-pay', 'POST'); } catch (e) {}
          toast('支付成功');
          this.load();
        },
        fail: () => toast('支付未完成')
      });
    } catch (e) { toast(e.message); }
  },

  async cancel() {
    const res = await wx.showModal({ title: '取消订单', content: '确定取消该订单吗？' });
    if (!res.confirm) return;
    try {
      await request('/api/orders/' + this.id + '/cancel', 'POST', { action: 'cancel', reason: '用户取消' });
      toast('已取消');
      this.load();
    } catch (e) { toast(e.message); }
  }
});
