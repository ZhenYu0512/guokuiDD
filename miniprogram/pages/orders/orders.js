const { request, toast } = require('../../utils/request');
const { fenToYuan, statusText, fmtTime } = require('../../utils/util');
const app = getApp();

const TABS = ['全部', '待支付', '烤制中', '待取餐', '已完成', '已取消'];

Page({
  data: { tabs: TABS, activeTab: 0, orders: [], loading: false },

  onShow() { this.load(); },

  async load() {
    try {
      await app.login();
      this.setData({ loading: true });
      const status = this.data.activeTab === 0 ? '' : '?status=' + (this.data.activeTab - 1);
      const orders = await request('/api/orders' + status);
      orders.forEach((o) => {
        o.payText = fenToYuan(o.payAmount);
        o.statusText = statusText(o.status);
        o.timeText = fmtTime(o.createdAt);
        o.summary = o.items.map((i) => i.productName + ' x' + i.quantity).join('、');
      });
      this.setData({ orders, loading: false });
    } catch (e) {
      this.setData({ loading: false });
      toast(e.message);
    }
  },

  onTabChange(e) {
    this.setData({ activeTab: Number(e.currentTarget.dataset.index) }, () => this.load());
  },

  goDetail(e) {
    wx.navigateTo({ url: '/pages/order-detail/order-detail?id=' + e.currentTarget.dataset.id });
  },

  async pay(e) {
    const id = e.currentTarget.dataset.id;
    try {
      const res = await request('/api/orders/' + id + '/pay', 'POST');
      if (res.mode === 'mock' || res.paid) {
        toast('支付成功，取餐码 ' + res.pickupCode);
        return this.load();
      }
      wx.requestPayment({
        ...res.payParams,
        success: async () => {
          try { await request('/api/orders/' + id + '/sync-pay', 'POST'); } catch (e) {}
          toast('支付成功');
          this.load();
        },
        fail: () => toast('支付未完成')
      });
    } catch (err) { toast(err.message); }
  },

  async cancel(e) {
    const id = e.currentTarget.dataset.id;
    const res = await wx.showModal({ title: '取消订单', content: '确定取消该订单吗？' });
    if (!res.confirm) return;
    try {
      await request('/api/orders/' + id + '/cancel', 'POST', { action: 'cancel', reason: '用户取消' });
      toast('已取消');
      this.load();
    } catch (err) { toast(err.message); }
  }
});
