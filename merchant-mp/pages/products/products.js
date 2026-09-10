const { request, toast, fen2yuan } = require('../../utils/request');
const app = getApp();

Page({
  data: { products: [], categories: [], loading: false },

  onShow() {
    if (!app.isLoggedIn()) return wx.reLaunch({ url: '/pages/login/login' });
    this.load();
  },

  async load() {
    this.setData({ loading: true });
    try {
      const res = await request('/api/admin/catalog');
      res.products.forEach((p) => (p.priceText = fen2yuan(p.basePrice)));
      const catMap = {};
      res.categories.forEach((c) => (catMap[c.id] = c.name));
      res.products.forEach((p) => (p.categoryName = catMap[p.categoryId] || '-'));
      this.setData({ products: res.products, categories: res.categories, loading: false });
    } catch (e) {
      this.setData({ loading: false });
      toast(e.message);
    }
  },

  onPullDownRefresh() {
    this.load().then(() => wx.stopPullDownRefresh());
  },

  async toggleSoldOut(e) {
    const { id, value } = e.currentTarget.dataset;
    try {
      await request('/api/admin/catalog/products/' + id + '/soldout', 'PATCH', { isSoldOut: value });
      toast(value ? '已设为售罄' : '已恢复售卖');
      this.load();
    } catch (err) { toast(err.message); }
  },

  async editStock(e) {
    const { id, stock, name } = e.currentTarget.dataset;
    const res = await wx.showModal({
      title: '修改库存：' + name,
      editable: true,
      placeholderText: '当前库存 ' + stock,
      content: String(stock)
    });
    if (!res.confirm) return;
    const newStock = parseInt(res.content);
    if (isNaN(newStock) || newStock < 0) return toast('请输入正确的库存数量');
    try {
      await request('/api/admin/catalog/products/' + id + '/stock', 'PATCH', { stock: newStock });
      toast('库存已更新');
      this.load();
    } catch (err) { toast(err.message); }
  },

  goAdd() { wx.navigateTo({ url: '/pages/product-edit/product-edit' }); }
});
