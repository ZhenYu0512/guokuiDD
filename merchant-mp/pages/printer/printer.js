const { request, toast } = require('../../utils/request');
const bt = require('../../utils/bluetooth');
const escpos = require('../../utils/escpos');
const app = getApp();

Page({
  data: {
    btConnected: false,
    btName: '',
    scanning: false,
    devices: [],
    printers: [],     // 后端绑定的打印机（云打印/网口/USB）
    printing: false
  },

  onShow() {
    if (!app.isLoggedIn()) return wx.reLaunch({ url: '/pages/login/login' });
    this.syncBtState();
    this.loadPrinters();
  },

  syncBtState() {
    this.setData({ btConnected: bt.isConnected(), btName: bt.currentName() });
  },

  async loadPrinters() {
    try {
      const printers = await request('/api/admin/printers');
      printers.forEach((p) => {
        p.connText = { cloud: '云打印', usb: '有线USB', lan: '网口' }[p.connType || 'cloud'];
        p.addrText = (p.connType || 'cloud') === 'cloud' ? (p.sn || '-') : (p.address || '-');
      });
      this.setData({ printers });
    } catch (e) { toast(e.message); }
  },

  // ---------- 蓝牙 ----------
  async scan() {
    this.setData({ scanning: true, devices: [] });
    try {
      await bt.ensureAdapter();
      const devices = await bt.scanDevices(6000);
      this.setData({ devices, scanning: false });
      if (!devices.length) toast('未发现设备，请确认打印机已开机');
    } catch (e) {
      this.setData({ scanning: false });
      toast(e.message);
    }
  },

  async connectDevice(e) {
    const device = e.currentTarget.dataset.device;
    wx.showLoading({ title: '连接中...' });
    try {
      await bt.connect(device);
      wx.hideLoading();
      this.syncBtState();
      toast('已连接 ' + (device.name || ''));
      this.setData({ devices: [] });
    } catch (err) {
      wx.hideLoading();
      toast(err.message);
    }
  },

  disconnectBt() {
    bt.disconnect();
    this.syncBtState();
    toast('已断开');
  },

  async testBtPrint() {
    if (this.data.printing) return;
    this.setData({ printing: true });
    try {
      const bytes = await escpos.buildTestPrintBytes(this.data.btName || '蓝牙打印机');
      await bt.writeBytes(bytes);
      toast('测试小票已发送');
    } catch (e) { toast(e.message); }
    this.setData({ printing: false });
  },

  // ---------- 云打印 / 网口 ----------
  async testCloudPrint(e) {
    const id = e.currentTarget.dataset.id;
    try {
      const res = await request('/api/admin/printers/' + id + '/test-print', 'POST');
      toast(res.message || '已发送');
    } catch (err) { toast(err.message); }
  }
});
