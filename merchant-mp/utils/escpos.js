// ESC/POS 光栅位图打印：把中文小票画到 Canvas 再转 1bit 位图下发
// 优势：不依赖打印机字库（多数 BLE 热敏机只有 GBK 字库，小程序端无 GBK 编码器），中文/表情均不乱码

const PAPER_WIDTH = 384; // 58mm 纸 203dpi = 384 点

// 组装小票排版行
function buildLines(order, storeName) {
  const L = [];
  const yuan = (f) => (f / 100).toFixed(2);
  L.push({ text: storeName, size: 30, bold: true, align: 'center', gap: 12 });
  L.push({ text: '--------------------------------', size: 20, align: 'center', gap: 6 });
  L.push({ text: '取餐码 ' + (order.pickupCode || '-'), size: 44, bold: true, align: 'center', gap: 14 });
  L.push({ text: '单号 ' + order.orderNo, size: 20, align: 'center', gap: 4 });
  L.push({ text: (order.createdAt || '').replace('T', ' ').substring(0, 16), size: 20, align: 'center', gap: 10 });
  L.push({ text: '--------------------------------', size: 20, align: 'center', gap: 8 });
  (order.items || []).forEach((i) => {
    L.push({ text: `${i.productName} x${i.quantity}  ¥${yuan(i.subtotal)}`, size: 22, bold: true, gap: 4 });
    if (i.specText) L.push({ text: '  ' + i.specText, size: 18, gap: 8 });
  });
  L.push({ text: '--------------------------------', size: 20, align: 'center', gap: 8 });
  L.push({ text: `商品金额 ¥${yuan(order.totalAmount)}`, size: 20, gap: 4 });
  if (order.couponDiscount > 0) L.push({ text: `优惠券 -¥${yuan(order.couponDiscount)}`, size: 20, gap: 4 });
  if (order.pointsDiscount > 0) L.push({ text: `积分抵扣 -¥${yuan(order.pointsDiscount)}`, size: 20, gap: 4 });
  L.push({ text: `实付 ¥${yuan(order.payAmount)}`, size: 26, bold: true, gap: 10 });
  L.push({ text: `${order.contactName}  ${order.contactPhone}`, size: 20, gap: 4 });
  if (order.remark) L.push({ text: '备注：' + order.remark, size: 20, gap: 8 });
  L.push({ text: '--------------------------------', size: 20, align: 'center', gap: 8 });
  L.push({ text: '请凭取餐码取餐', size: 24, bold: true, align: 'center', gap: 10 });
  return L;
}

// Canvas 绘制并转 1bit 位图（MSB first，每行 width/8 字节）
async function renderToMono(lines) {
  const lineH = (l) => Math.ceil(l.size * 1.4) + (l.gap || 0);
  const height = lines.reduce((s, l) => s + lineH(l), 0) + 20;

  const canvas = wx.createOffscreenCanvas({ type: '2d', width: PAPER_WIDTH, height });
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, PAPER_WIDTH, height);

  let y = 10;
  lines.forEach((l) => {
    ctx.fillStyle = '#000000';
    ctx.font = `${l.bold ? 'bold ' : ''}${l.size}px sans-serif`;
    ctx.textAlign = l.align === 'center' ? 'center' : 'left';
    ctx.textBaseline = 'top';
    y += l.size * 0.2;
    ctx.fillText(l.text, l.align === 'center' ? PAPER_WIDTH / 2 : 8, y);
    y += l.size * 1.2 + (l.gap || 0);
  });

  const img = ctx.getImageData(0, 0, PAPER_WIDTH, height);
  const bytesPerRow = PAPER_WIDTH / 8;
  const mono = new Uint8Array(bytesPerRow * height);
  for (let yy = 0; yy < height; yy++) {
    for (let xx = 0; xx < PAPER_WIDTH; xx++) {
      const idx = (yy * PAPER_WIDTH + xx) * 4;
      const gray = img.data[idx] + img.data[idx + 1] + img.data[idx + 2];
      if (gray < 384) mono[yy * bytesPerRow + (xx >> 3)] |= 0x80 >> (xx & 7);
    }
  }
  return { mono, height, bytesPerRow };
}

// 打包 ESC/POS 指令：初始化 + GS v 0 光栅 + 走纸
function buildCommands(monoData) {
  const { mono, height, bytesPerRow } = monoData;
  const head = [
    0x1b, 0x40, // 初始化
    0x1d, 0x76, 0x30, 0x00, // GS v 0 普通模式
    bytesPerRow & 0xff, (bytesPerRow >> 8) & 0xff,
    height & 0xff, (height >> 8) & 0xff
  ];
  const tail = [0x0a, 0x0a, 0x0a, 0x0a, 0x0a];
  const out = new Uint8Array(head.length + mono.length + tail.length);
  out.set(head, 0);
  out.set(mono, head.length);
  out.set(tail, head.length + mono.length);
  return out;
}

// 对外：订单 → 可打印字节流
async function buildOrderPrintBytes(order, storeName) {
  const lines = buildLines(order, storeName);
  const monoData = await renderToMono(lines);
  return buildCommands(monoData);
}

// 对外：测试小票
async function buildTestPrintBytes(printerName) {
  const lines = [
    { text: '锅盔肉夹馍 测试小票', size: 30, bold: true, align: 'center', gap: 14 },
    { text: '--------------------------------', size: 20, align: 'center', gap: 8 },
    { text: '打印机：' + printerName, size: 22, align: 'center', gap: 8 },
    { text: '中文打印测试：麻辣鲜香 现烤现卖', size: 22, align: 'center', gap: 8 },
    { text: new Date().toLocaleString(), size: 20, align: 'center', gap: 12 },
    { text: '打印正常则连接成功', size: 24, bold: true, align: 'center', gap: 10 }
  ];
  const monoData = await renderToMono(lines);
  return buildCommands(monoData);
}

module.exports = { buildOrderPrintBytes, buildTestPrintBytes };
