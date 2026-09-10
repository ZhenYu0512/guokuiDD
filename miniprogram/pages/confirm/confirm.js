const { request, toast } = require('../../utils/request');
const { fenToYuan } = require('../../utils/util');
const app = getApp();

Page({
  data: {
    cart: [],
    totalText: '0.00',
    contactName: '',
    contactPhone: '',
    remark: '',
    // 优惠券
    coupons: [],
    couponLabels: [],
    couponIndex: -1,
    couponDiscountText: '0.00',
    // 积分
    userPoints: 0,
    usePoints: 0,
    pointsDiscountText: '0.00',
    // 应付
    payText: '0.00',
    submitting: false
  },

  onLoad() {
    const cart = wx.getStorageSync('cart') || [];
    if (!cart.length) {
      toast('购物车为空');
      return setTimeout(() => wx.navigateBack(), 800);
    }
    cart.forEach((c) => (c.subtotalText = fenToYuan(c.unitPrice * c.quantity)));
    this.setData({ cart, totalText: fenToYuan(this.total()) });
    const contact = wx.getStorageSync('contact') || {};
    this.setData({ contactName: contact.name || '', contactPhone: contact.phone || '' });
    this.initUser();
  },

  total() {
    return this.data.cart.reduce((s, c) => s + c.unitPrice * c.quantity, 0);
  },

  async initUser() {
    try {
      await app.login();
      const me = await request('/api/users/me');
      this.setData({ userPoints: me.points });
      const coupons = await request('/api/users/me/coupons/usable?totalAmount=' + this.total());
      const couponLabels = ['不使用优惠券'].concat(
        coupons.map((c) => c.name + '（减¥' + (c.discountAmount / 100).toFixed(2) + '）')
      );
      this.setData({ coupons, couponLabels });
      this.recalc();
    } catch (e) { toast(e.message); }
  },

  onNameInput(e) { this.setData({ contactName: e.detail.value }); },
  onPhoneInput(e) { this.setData({ contactPhone: e.detail.value }); },
  onRemarkInput(e) { this.setData({ remark: e.detail.value }); },

  onCouponChange(e) {
    this.setData({ couponIndex: Number(e.detail.value) - 1 }); // 第0项为「不使用」
    this.recalc();
  },

  onPointsInput(e) {
    let v = parseInt(e.detail.value) || 0;
    const maxPoints = Math.min(this.data.userPoints, this.total() - this.couponDiscount());
    v = Math.max(0, Math.min(v, maxPoints));
    this.setData({ usePoints: v });
    this.recalc();
  },

  couponDiscount() {
    const c = this.data.coupons[this.data.couponIndex];
    return c ? c.discountAmount : 0;
  },

  // 前端仅作展示预估，最终金额以后端重算为准
  recalc() {
    const discount = this.couponDiscount();
    const pay = Math.max(0, this.total() - discount - this.data.usePoints);
    this.setData({
      couponDiscountText: fenToYuan(discount),
      pointsDiscountText: fenToYuan(this.data.usePoints),
      payText: fenToYuan(pay)
    });
  },

  async submit() {
    if (this.data.submitting) return;
    if (!this.data.contactName.trim()) return toast('请填写取餐人姓名');
    if (!/^1\d{10}$/.test(this.data.contactPhone)) return toast('请填写正确的手机号');
    this.setData({ submitting: true });
    wx.setStorageSync('contact', { name: this.data.contactName, phone: this.data.contactPhone });

    try {
      const coupon = this.data.coupons[this.data.couponIndex];
      // 1. 创建订单（金额由后端重算）
      const order = await request('/api/orders', 'POST', {
        storeId: app.globalData.storeId,
        items: this.data.cart.map((c) => ({
          productId: c.productId,
          specOptionIds: c.specOptionIds,
          addonIds: c.addonIds,
          quantity: c.quantity
        })),
        userCouponId: coupon ? coupon.id : null,
        pointsUsed: this.data.usePoints,
        contactName: this.data.contactName,
        contactPhone: this.data.contactPhone,
        remark: this.data.remark
      });

      // 2. 发起支付（微信支付 V3 / 开发 Mock）
      await this.pay(order.id);
    } catch (e) {
      toast(e.message);
      this.setData({ submitting: false });
    }
  },

  async pay(orderId) {
    const res = await request('/api/orders/' + orderId + '/pay', 'POST');
    if (res.mode === 'mock' || res.paid) {
      return this.onPaid();
    }
    // 调起微信支付
    wx.requestPayment({
      ...res.payParams,
      success: async () => {
        // 支付成功：主动对账确认（回调不可达时的兜底，幂等）
        try { await request('/api/orders/' + orderId + '/sync-pay', 'POST'); } catch (e) {}
        this.onPaid();
      },
      fail: (err) => {
        this.setData({ submitting: false });
        if (err.errMsg && err.errMsg.includes('cancel')) {
          toast('支付已取消，可在订单页继续支付');
        } else {
          toast('支付失败');
        }
        wx.switchTab({ url: '/pages/orders/orders' });
      }
    });
  },

  onPaid() {
    wx.removeStorageSync('cart');
    toast('支付成功');
    wx.switchTab({ url: '/pages/orders/orders' });
  }
});
