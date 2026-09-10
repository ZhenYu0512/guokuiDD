const { request, toast } = require('../../utils/request');
const { fenToYuan } = require('../../utils/util');
const app = getApp();

Page({
  data: {
    categories: [],
    addons: [],
    activeCat: 0,          // 当前选中分类索引
    scrollInto: '',        // 右侧滚动联动
    cart: [],              // {key, productId, name, image, specs[], addons[], unitPrice, quantity}
    cartCount: 0,
    cartTotalText: '0.00',
    // 规格弹窗
    showSpec: false,
    current: null,         // 当前选规格的商品
    selectedSpecs: {},     // {groupId: optionId}
    popupAddons: [],       // 弹窗内加料（含 checked 状态）
    quantity: 1
  },

  onShow() {
    this.loadMenu();
    const cart = wx.getStorageSync('cart') || [];
    this.setCart(cart);
  },

  loadMenu() {
    request('/api/menu?storeId=' + app.globalData.storeId).then((res) => {
      res.categories.forEach((c) => c.products.forEach((p) => (p.priceText = fenToYuan(p.basePrice))));
      res.addons.forEach((a) => (a.priceText = fenToYuan(a.price)));
      this.setData({ categories: res.categories, addons: res.addons });
    }).catch((e) => toast(e.message));
  },

  // 左侧分类点击 → 右侧滚动到对应区域
  tapCat(e) {
    const i = e.currentTarget.dataset.index;
    this.setData({ activeCat: i, scrollInto: 'cat-' + i });
  },

  // 右侧滚动 → 左侧高亮联动
  onRightScroll(e) {
    // 简化实现：滚动结束后由 scroll-view 的 scroll-into-view 驱动，这里不做反向计算
  },

  // 打开规格弹窗
  openSpec(e) {
    const p = e.currentTarget.dataset.product;
    if (p.isSoldOut) return;
    const selectedSpecs = {};
    p.specGroups.forEach((g) => {
      if (g.isRequired && g.options.length) selectedSpecs[g.id] = g.options[0].id; // 默认第一项
    });
    const popupAddons = this.data.addons.map((a) => ({ ...a, checked: false }));
    this.setData({ showSpec: true, current: p, selectedSpecs, popupAddons, quantity: 1 });
  },

 closeSpec() { this.setData({ showSpec: false }); },

  pickSpec(e) {
    console.log(e.currentTarget.dataset)
    const { group, option } = e.currentTarget.dataset;
    this.setData({ ['selectedSpecs.' + group]: option });
  },

  toggleAddon(e) {
    const index = e.currentTarget.dataset.index;
    const popupAddons = this.data.popupAddons.slice();
    const checkedCount = popupAddons.filter((a) => a.checked).length;
    if (!popupAddons[index].checked && checkedCount >= 3) return toast('加料最多选择 3 个');
    popupAddons[index].checked = !popupAddons[index].checked;
    this.setData({ popupAddons });
  },

  selectedAddons() {
    return this.data.popupAddons.filter((a) => a.checked);
  },

  changeQty(e) {
    const delta = e.currentTarget.dataset.delta;
    const q = Math.max(1, this.data.quantity + delta);
    this.setData({ quantity: q });
  },

  // 当前弹窗单价（展示用，金额以后端重算为准）
  currentUnitPrice() {
    const p = this.data.current;
    if (!p) return 0;
    let price = p.basePrice;
    p.specGroups.forEach((g) => {
      const oid = this.data.selectedSpecs[g.id];
      const opt = g.options.find((o) => o.id === oid);
      if (opt) price += opt.priceDelta;
    });
    this.selectedAddons().forEach((a) => { price += a.price; });
    return price;
  },

  addToCart() {
    const p = this.data.current;
    // 校验必选规格
    for (const g of p.specGroups) {
      if (g.isRequired && !this.data.selectedSpecs[g.id]) return toast('请选择' + g.name);
    }
    const specIds = Object.values(this.data.selectedSpecs);
    const addonIds = this.selectedAddons().map((a) => a.id);
    const key = p.id + '-' + specIds.sort().join(',') + '-' + addonIds.sort().join(',');

    const specTexts = [];
    p.specGroups.forEach((g) => {
      const opt = g.options.find((o) => o.id === this.data.selectedSpecs[g.id]);
      if (opt) specTexts.push(opt.name);
    });
    const addonTexts = this.data.addons.filter((a) => addonIds.includes(a.id)).map((a) => a.name);

    const cart = this.data.cart.slice();
    const exist = cart.find((c) => c.key === key);
    if (exist) exist.quantity += this.data.quantity;
    else cart.push({
      key, productId: p.id, name: p.name, image: p.imageUrl,
      specOptionIds: specIds, addonIds,
      specText: specTexts.join('/'), addonText: addonTexts.join('/'),
      unitPrice: this.currentUnitPrice(), quantity: this.data.quantity
    });
    this.setCart(cart);
    this.setData({ showSpec: false });
    toast('已加入购物车');
  },

  setCart(cart) {
    const count = cart.reduce((s, c) => s + c.quantity, 0);
    const total = cart.reduce((s, c) => s + c.unitPrice * c.quantity, 0);
    this.setData({ cart, cartCount: count, cartTotalText: fenToYuan(total) });
    wx.setStorageSync('cart', cart);
  },

  clearCart() { this.setCart([]); },

  goConfirm() {
    if (!this.data.cart.length) return toast('请先选择商品');
    wx.navigateTo({ url: '/pages/confirm/confirm' });
  }
});
