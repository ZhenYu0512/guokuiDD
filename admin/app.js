const { createApp } = Vue;
const { ElMessage, ElMessageBox } = ElementPlus;

createApp({
  data() {
    return {
      token: apiToken(),
      loading: false,
      loginForm: { username: '', password: '' },
      page: 'dashboard',
      stats: {},
      catalog: { categories: [], products: [], specGroups: [], specOptions: [], addons: [] },
      catalogTab: 'products',
      orders: [],
      orderStatusTab: '-1',
      orderDate: '',
      alerts: [],
      alertThreshold: 10,
      printers: [],
      coupons: [],
      categoryDialog: false,
      categoryForm: {},
      productDialog: false,
      productForm: {},
      addonDialog: false,
      addonForm: {},
      printerDialog: false,
      printerForm: {},
      specGroupDialog: false,
      specGroupForm: {},
      couponDialog: false,
      couponForm: { name: '', thresholdYuan: 0, discountYuan: 5, range: [], totalCount: 100 },
      aboutDialog: false,
      uploading: false
    };
  },
  watch: {
    page(p) {
      if (p === 'dashboard') this.loadStats();
      if (p === 'catalog') this.loadCatalog();
      if (p === 'orders') this.loadOrders();
      if (p === 'alerts') this.loadAlerts();
      if (p === 'printers') this.loadPrinters();
      if (p === 'coupons') this.loadCoupons();
    }
  },
  mounted() { if (this.token) this.loadStats(); },
  methods: {
    fen2yuan(f) { return ((f || 0) / 100).toFixed(2); },
    fmtTime(s) { return s ? String(s).replace('T', ' ').substring(0, 16) : ''; },
    yuan2fen(y) { return Math.round((y || 0) * 100); },
    statusText(s) { return ['待支付', '烤制中', '待取餐', '已完成', '已取消'][s] || '未知'; },
    statusTagType(s) { return ['warning', 'danger', 'primary', 'success', 'info'][s]; },
    categoryName(id) { const c = this.catalog.categories.find((x) => x.id === id); return c ? c.name : '-'; },
    productName(id) { const p = this.catalog.products.find((x) => x.id === id); return p ? p.name : '-'; },
    specOptionsOf(gid) { return this.catalog.specOptions.filter((o) => o.specGroupId === gid); },
    specSummary(pid) {
      return this.catalog.specGroups
        .filter((g) => g.productId === pid)
        .map((g) => g.name + '(' + this.specOptionsOf(g.id).map((o) => o.name).join('/') + ')')
        .join('；');
    },
    specText(item) {
      try {
        const specs = JSON.parse(item.specsJson || '[]').map((s) => s.option).join('/');
        const addons = JSON.parse(item.addonsJson || '[]').map((a) => a.name).join('+');
        return [specs, addons].filter(Boolean).join(' / ');
      } catch { return ''; }
    },

    async login() {
      this.loading = true;
      try {
        const res = await apiPost('/api/admin/auth/login', this.loginForm);
        localStorage.setItem('admin_token', res.token);
        this.token = res.token;
        this.loadStats();
      } catch (e) { ElMessage.error(e.message); }
      this.loading = false;
    },
    logout() { localStorage.removeItem('admin_token'); location.reload(); },

    async loadStats() { this.stats = await apiGet('/api/admin/orders/stats'); },

    // ---------- 商品管理 ----------
    async loadCatalog() { this.catalog = await apiGet('/api/admin/catalog'); },
    openCategoryDialog(row) {
      this.categoryForm = row ? { ...row } : { id: 0, storeId: 1, name: '', sort: 0, isActive: true };
      this.categoryDialog = true;
    },
    async saveCategory() {
      const f = this.categoryForm;
      const body = { storeId: 1, name: f.name, sort: f.sort, isActive: f.isActive };
      if (f.id) await apiPut('/api/admin/catalog/categories/' + f.id, body);
      else await apiPost('/api/admin/catalog/categories', body);
      ElMessage.success('已保存');
      this.categoryDialog = false;
      this.loadCatalog();
    },
    async removeCategory(row) {
      await ElMessageBox.confirm('确定删除分类「' + row.name + '」？', '提示', { type: 'warning' });
      try {
        await apiDel('/api/admin/catalog/categories/' + row.id);
        ElMessage.success('已删除');
        this.loadCatalog();
      } catch (e) { ElMessage.error(e.message); }
    },
    openProductDialog(row) {
      this.productForm = row
        ? { ...row, priceYuan: row.basePrice / 100 }
        : { id: 0, categoryId: null, name: '', imageUrl: '', description: '', priceYuan: 10, stock: 100, sort: 0, isFeatured: false, isSoldOut: false, isActive: true };
      this.productDialog = true;
    },
    async saveProduct() {
      const f = this.productForm;
      const body = {
        categoryId: f.categoryId, name: f.name, imageUrl: f.imageUrl, description: f.description,
        basePrice: this.yuan2fen(f.priceYuan), stock: f.stock, isSoldOut: f.isSoldOut,
        isFeatured: f.isFeatured, sort: f.sort, isActive: f.isActive
      };
      if (f.id) await apiPut('/api/admin/catalog/products/' + f.id, body);
      else await apiPost('/api/admin/catalog/products', body);
      ElMessage.success('已保存');
      this.productDialog = false;
      this.loadCatalog();
    },
    async removeProduct(row) {
      await ElMessageBox.confirm('下架商品「' + row.name + '」？（订单快照不受影响）', '提示', { type: 'warning' });
      await apiDel('/api/admin/catalog/products/' + row.id);
      ElMessage.success('已下架');
      this.loadCatalog();
    },
    async toggleSoldOut(row, val) {
      await apiPatch('/api/admin/catalog/products/' + row.id + '/soldout', { isSoldOut: val });
      row.isSoldOut = val;
      ElMessage.success(val ? '已设为售罄' : '已恢复售卖');
    },
    openAddonDialog(row) {
      this.addonForm = row ? { ...row, priceYuan: row.price / 100 } : { id: 0, storeId: 1, name: '', priceYuan: 1, isActive: true };
      this.addonDialog = true;
    },
    async saveAddon() {
      const f = this.addonForm;
      const body = { storeId: 1, name: f.name, price: this.yuan2fen(f.priceYuan), isActive: f.isActive };
      if (f.id) await apiPut('/api/admin/catalog/addons/' + f.id, body);
      else await apiPost('/api/admin/catalog/addons', body);
      ElMessage.success('已保存');
      this.addonDialog = false;
      this.loadCatalog();
    },
    async removeAddon(row) {
      await ElMessageBox.confirm('删除加料「' + row.name + '」？', '提示', { type: 'warning' });
      await apiDel('/api/admin/catalog/addons/' + row.id);
      this.loadCatalog();
    },
    openSpecGroupDialog(productId) {
      this.specGroupForm = { productId: productId || null, name: '辣度', isRequired: true };
      this.specGroupDialog = true;
    },
    async saveSpecGroup() {
      const f = this.specGroupForm;
      if (!f.productId) return ElMessage.error('请选择商品');
      if (!f.name.trim()) return ElMessage.error('请输入组名');
      await apiPost('/api/admin/catalog/spec-groups', { productId: f.productId, name: f.name, isRequired: f.isRequired, sort: 1 });
      ElMessage.success('已添加');
      this.specGroupDialog = false;
      this.loadCatalog();
    },
    async removeSpecGroup(row) {
      await ElMessageBox.confirm('删除规格组「' + row.name + '」及其所有选项？', '提示', { type: 'warning' });
      await apiDel('/api/admin/catalog/spec-groups/' + row.id);
      this.loadCatalog();
    },
    async addSpecOption(group) {
      const { value } = await ElMessageBox.prompt('格式：名称 或 名称|加价元（如 特辣 或 加肉|3）', '新增选项');
      const [name, price] = value.split('|');
      await apiPost('/api/admin/catalog/spec-options', {
        specGroupId: group.id, name: name.trim(), priceDelta: this.yuan2fen(parseFloat(price) || 0), sort: 99
      });
      this.loadCatalog();
    },
    async removeSpecOption(o) {
      await apiDel('/api/admin/catalog/spec-options/' + o.id);
      this.loadCatalog();
    },

    // ---------- 订单管理 ----------
    async loadOrders() {
      const params = [];
      if (this.orderStatusTab !== '-1') params.push('status=' + this.orderStatusTab);
      if (this.orderDate) params.push('date=' + this.orderDate);
      this.orders = await apiGet('/api/admin/orders' + (params.length ? '?' + params.join('&') : ''));
    },
    async changeStatus(row, action) {
      await apiPost('/api/admin/orders/' + row.id + '/status', { action });
      ElMessage.success('操作成功');
      this.loadOrders();
    },
    async cancelOrder(row) {
      const { value } = await ElMessageBox.prompt('请输入取消原因（已支付订单将自动退款）', '取消订单', { inputValue: '商家取消' });
      try {
        await apiPost('/api/admin/orders/' + row.id + '/status', { action: 'cancel', reason: value });
        ElMessage.success('已取消');
        this.loadOrders();
      } catch (e) { ElMessage.error(e.message); }
    },

    // ---------- 库存预警 ----------
    async loadAlerts() { this.alerts = await apiGet('/api/admin/inventory/alerts?threshold=' + this.alertThreshold); },
    async restock(row) {
      const { value } = await ElMessageBox.prompt('请输入新的库存数量', '补充库存：' + row.name, { inputValue: '100' });
      await apiPatch('/api/admin/catalog/products/' + row.id + '/stock', { stock: Number(value) });
      ElMessage.success('已更新库存');
      this.loadAlerts();
    },

    // ---------- 打印机 ----------
    async loadPrinters() { this.printers = await apiGet('/api/admin/printers'); },
    openPrinterDialog(row) {
      this.printerForm = row
        ? { ...row, connType: row.connType || 'cloud', statusBool: row.status === 1 }
        : { id: 0, storeId: 1, name: '', connType: 'cloud', sn: '', appKey: '', address: '', statusBool: false };
      this.printerDialog = true;
    },
    async savePrinter() {
      const f = this.printerForm;
      const body = {
        storeId: 1, name: f.name, connType: f.connType,
        sn: f.sn || '', appKey: f.appKey, address: f.address, status: f.statusBool ? 1 : 0
      };
      try {
        if (f.id) await apiPut('/api/admin/printers/' + f.id, body);
        else await apiPost('/api/admin/printers', body);
        ElMessage.success('已保存');
        this.printerDialog = false;
        this.loadPrinters();
      } catch (e) { ElMessage.error(e.message); }
    },
    async testPrint(row) {
      try {
        const res = await apiPost('/api/admin/printers/' + row.id + '/test-print');
        ElMessage.success(res.message);
      } catch (e) { ElMessage.error(e.message); }
    },
    async removePrinter(row) {
      await ElMessageBox.confirm('解绑打印机「' + row.name + '」？', '提示', { type: 'warning' });
      await apiDel('/api/admin/printers/' + row.id);
      this.loadPrinters();
    },

    // ---------- 优惠券 ----------
    async loadCoupons() { this.coupons = await apiGet('/api/admin/coupons'); },
    async saveCoupon() {
      const f = this.couponForm;
      if (!f.range || f.range.length !== 2) return ElMessage.error('请选择有效期');
      await apiPost('/api/admin/coupons', {
        name: f.name,
        thresholdAmount: this.yuan2fen(f.thresholdYuan),
        discountAmount: this.yuan2fen(f.discountYuan),
        validFrom: f.range[0] + 'T00:00:00',
        validTo: f.range[1] + 'T23:59:59',
        totalCount: f.totalCount,
        isActive: true
      });
      ElMessage.success('已创建');
      this.couponDialog = false;
      this.loadCoupons();
    },
    async toggleCoupon(row) {
      const res = await apiPatch('/api/admin/coupons/' + row.id + '/toggle');
      row.isActive = res.isActive;
    },
    beforeImageUpload(rawFile) {
      const allow = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'];
      if (!allow.includes(rawFile.type)) {
        ElMessage.error('只支持 jpg / png / gif / webp / bmp 格式');
        return false;
      }
      if (rawFile.size > 5 * 1024 * 1024) {
        ElMessage.error('图片不能超过 5MB');
        return false;
      }
      return true;
    },

    // el-upload 自定义上传（:http-request）。
    // 不用 el-upload 自带的 action，是为了走统一的 apiUpload（带 token、统一报错）。
    // options.file 就是选中的 File 对象。
    async uploadProductImage(options) {
      try {
        const res = await apiUpload('/api/admin/upload/image', options.file, 'products');
        // 后端返回 { url, key }，把 url 填进表单，保存商品时一并入库
        this.productForm.imageUrl = res.url;
        ElMessage.success('上传成功');
      } catch (e) {
        ElMessage.error(e.message);
      }
    },

    // 手动清空已选图片（换图时可用；旧文件不删，避免误删影响历史订单快照）
    clearProductImage() {
      this.productForm.imageUrl = '';
    },

    // 可选：一键检测对象存储是否连通，排查时很有用
    async testUpload() {
      try {
        const res = await apiGet('/api/admin/upload/test');
        if (res.success) ElMessage.success('对象存储连接正常');
        else ElMessage.error('连接失败：' + res.message + '｜' + (res.hint || ''));
      } catch (e) {
        ElMessage.error(e.message);
      }
    },
  }
  
}).use(ElementPlus).mount('#app');
