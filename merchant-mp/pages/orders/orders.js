const { request, toast, fen2yuan } = require('../../utils/request');
const bt = require('../../utils/bluetooth');
const escpos = require('../../utils/escpos');
const app = getApp();

const TABS = ['烤制中', '待取餐', '待支付', '已完成', '已取消'];
const STATUS_TEXT = ['待支付', '烤制中', '待取餐', '已完成', '已取消'];

Page({
  data: { tabs: TABS, activeTab: 0, orders: [], loading: false, username: '' },

  onShow() {
    if (!app.isLoggedIn()) return wx.reLaunch({ url: '/pages/login/login' });
    this.setData({ username: app.globalData.username });
    this.load();
  },

  async load() {
    // tab 映射：0->烤制中(1) 1->待取餐(2) 2->待支付(0) 3->已完成(3) 4->已取消(4)
    const statusMap = [1, 2, 0, 3, 4];
    this.setData({ loading: true });
    try {
      const orders = await request('/api/admin/orders?status=' + statusMap[this.data.activeTab]);
      orders.forEach((o) => {
        o.payText = fen2yuan(o.payAmount);
        o.statusText = STATUS_TEXT[o.status];
        o.timeText = (o.createdAt || '').replace('T', ' ').substring(5, 16);
        o.items.forEach((i) => {
          try {
            const specs = JSON.parse(i.specsJson || '[]').map((s) => s.option).join('/');
            const addons = JSON.parse(i.addonsJson || '[]').map((a) => a.name).join('+');
            i.specText = [specs, addons].filter(Boolean).join(' / ');
          } catch (e) { i.specText = ''; }
        });
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

  onPullDownRefresh() {
    this.load().then(() => wx.stopPullDownRefresh());
  },

  async action(e) {
    const { id, act, name } = e.currentTarget.dataset;
    const tips = { ready: '确认出餐，转为待取餐？', complete: '确认顾客已取餐？', cancel: '取消该订单？已支付订单将自动退款' };
    const res = await wx.showModal({ title: name, content: tips[act] });
    if (!res.confirm) return;
    try {
      await request('/api/admin/orders/' + id + '/status', 'POST', { action: act, reason: act === 'cancel' ? '商家取消' : null });
      toast('操作成功');
      this.load();
    } catch (err) { toast(err.message); }
  },

  logout() {
    app.logout();
  },

  // 打印小票：优先蓝牙（已连接时）；否则选择已绑定的云/网口打印机由后端下发
  async printOrder(e) {
    const order = e.currentTarget.dataset.order;
    if (bt.isConnected()) {
      wx.showLoading({ title: '打印中...' });
      try {
        const bytes = await escpos.buildOrderPrintBytes(order, '锅盔肉夹馍');
        await bt.writeBytes(bytes);
        wx.hideLoading();
        toast('已打印');
      } catch (err) {
        wx.hideLoading();
        toast(err.message);
      }
      return;
    }
    // 未连蓝牙：选择后端绑定的打印机
    try {
      const printers = (await request('/api/admin/printers'))
        .filter((p) => (p.connType || 'cloud') !== 'usb');
      if (!printers.length) {
        return toast('未连接蓝牙且无可用打印机，请先到「打印」页连接');
      }
      const names = printers.map((p) => p.name + '（' + ({ cloud: '云打印', lan: '网口' }[p.connType || 'cloud']) + '）');
      const res = await wx.showActionSheet({ itemList: names });
      const printer = printers[res.tapIndex];
      await request('/api/admin/orders/' + order.id + '/print', 'POST', { printerId: printer.id });
      toast('已发送到「' + printer.name + '」');
    } catch (err) {
      if (err && err.errMsg && err.errMsg.includes('cancel')) return;
      toast(err.message || '打印失败');
    }
  }
});
