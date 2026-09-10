const { request, toast } = require('../../utils/request');
const app = getApp();

Page({
  data: {
    categories: [],
    categoryIndex: -1,
    name: '',
    price: '',
    stock: 100,
    description: '',
    isFeatured: false,
    imageUrl: '',      // 已上传的图片 URL
    localImage: '',    // 本地待上传路径
    uploading: false,
    submitting: false
  },

  async onLoad() {
    try {
      const res = await request('/api/admin/catalog');
      this.setData({ categories: res.categories });
    } catch (e) { toast(e.message); }
  },

  onCategoryChange(e) { this.setData({ categoryIndex: Number(e.detail.value) }); },
  onNameInput(e) { this.setData({ name: e.detail.value }); },
  onPriceInput(e) { this.setData({ price: e.detail.value }); },
  onStockInput(e) { this.setData({ stock: parseInt(e.detail.value) || 0 }); },
  onDescInput(e) { this.setData({ description: e.detail.value }); },
  onFeaturedChange(e) { this.setData({ isFeatured: e.detail.value }); },

  // 选择图片（占位实现：选择后立即上传到后端 /uploads；
  // 后续接入对象存储时仅替换后端保存逻辑，前端无需改动）
  chooseImage() {
    wx.chooseMedia({
      count: 1,
      mediaType: ['image'],
      success: (res) => {
        const path = res.tempFiles[0].tempFilePath;
        this.setData({ localImage: path });
        this.uploadImage(path);
      }
    });
  },

  uploadImage(path) {
    this.setData({ uploading: true });
    wx.uploadFile({
      url: app.globalData.baseUrl + '/api/admin/upload/image?folder=products',
      filePath: path,
      name: 'file',
      header: { Authorization: 'Bearer ' + app.globalData.token },
      success: (res) => {
        this.setData({ uploading: false });
        try {
          const data = JSON.parse(res.data);
          if (res.statusCode === 200 && data.url) {
            this.setData({ imageUrl: data.url });
            toast('图片已上传');
          } else {
            toast(data.message || '上传失败');
          }
        } catch (e) { toast('上传失败'); }
      },
      fail: () => {
        this.setData({ uploading: false });
        toast('上传失败，请检查网络');
      }
    });
  },

  async submit() {
    const d = this.data;
    if (d.categoryIndex < 0) return toast('请选择分类');
    if (!d.name.trim()) return toast('请输入商品名称');
    const price = parseFloat(d.price);
    if (isNaN(price) || price <= 0) return toast('请输入正确价格');
    if (d.submitting || d.uploading) return;
    this.setData({ submitting: true });
    try {
      await request('/api/admin/catalog/products', 'POST', {
        categoryId: d.categories[d.categoryIndex].id,
        name: d.name.trim(),
        imageUrl: d.imageUrl, // 为空时后端自动使用占位图
        description: d.description,
        basePrice: Math.round(price * 100), // 元转分
        stock: d.stock,
        isSoldOut: false,
        isFeatured: d.isFeatured,
        sort: 99,
        isActive: true
      });
      toast('添加成功');
      setTimeout(() => wx.navigateBack(), 600);
    } catch (e) {
      toast(e.message);
      this.setData({ submitting: false });
    }
  }
});
