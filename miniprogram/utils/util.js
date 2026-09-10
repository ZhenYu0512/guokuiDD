// 金额（分）格式化工具
function fenToYuan(fen) {
  return (fen / 100).toFixed(2);
}

const ORDER_STATUS = ['待支付', '烤制中', '待取餐', '已完成', '已取消'];

function statusText(s) {
  return ORDER_STATUS[s] || '未知';
}

function fmtTime(str) {
  if (!str) return '';
  return str.replace('T', ' ').substring(0, 16);
}

module.exports = { fenToYuan, statusText, fmtTime };
