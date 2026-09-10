// 低功耗蓝牙（BLE）热敏小票机连接管理
// 兼容常见 BLE 打印机：自动发现可写特征值（write / writeWithoutResponse）

let conn = null; // { deviceId, name, serviceId, charId, writeType }

function isConnected() {
  return !!conn;
}

function currentName() {
  return conn ? conn.name : '';
}

function ensureAdapter() {
  return new Promise((resolve, reject) => {
    wx.openBluetoothAdapter({
      success: resolve,
      fail: (e) => reject(new Error(e.errCode === 10001 ? '请打开手机蓝牙后重试' : '蓝牙初始化失败：' + e.errMsg))
    });
  });
}

// 搜索附近 BLE 设备，timeout 毫秒后返回去重列表
function scanDevices(timeout = 6000) {
  const found = new Map();
  return new Promise((resolve, reject) => {
    wx.onBluetoothDeviceFound((res) => {
      res.devices.forEach((d) => {
        if (d.name && !found.has(d.deviceId)) found.set(d.deviceId, d);
      });
    });
    wx.startBluetoothDevicesDiscovery({
      allowDuplicatesKey: false,
      success: () => {
        setTimeout(() => {
          wx.stopBluetoothDevicesDiscovery();
          resolve([...found.values()]);
        }, timeout);
      },
      fail: (e) => reject(new Error('搜索失败：' + e.errMsg))
    });
  });
}

function _createConnection(deviceId) {
  return new Promise((resolve, reject) => {
    wx.createBLEConnection({ deviceId, success: resolve, fail: (e) => reject(new Error('连接失败：' + e.errMsg)) });
  });
}

function _getServices(deviceId) {
  return new Promise((resolve, reject) => {
    wx.getBLEDeviceServices({ deviceId, success: (r) => resolve(r.services), fail: reject });
  });
}

function _getCharacteristics(deviceId, serviceId) {
  return new Promise((resolve, reject) => {
    wx.getBLEDeviceCharacteristics({ deviceId, serviceId, success: (r) => resolve(r.characteristics), fail: reject });
  });
}

// 连接并自动寻找可写特征值
async function connect(device) {
  await ensureAdapter();
  if (conn) disconnect();
  await _createConnection(device.deviceId);
  const services = await _getServices(device.deviceId);
  for (const s of services) {
    const chars = await _getCharacteristics(device.deviceId, s.uuid);
    const w = chars.find((c) => c.properties.write || c.properties.writeNoResponse);
    if (w) {
      conn = {
        deviceId: device.deviceId,
        name: device.name || '未知设备',
        serviceId: s.uuid,
        charId: w.uuid,
        writeType: w.properties.writeNoResponse ? 'writeNoResponse' : 'write'
      };
      wx.setStorageSync('bt_printer', { deviceId: device.deviceId, name: device.name });
      wx.onBLEConnectionStateChange((res) => {
        if (!res.connected && conn && res.deviceId === conn.deviceId) conn = null;
      });
      return conn;
    }
  }
  wx.closeBLEConnection({ deviceId: device.deviceId });
  throw new Error('未找到可写的打印特征值，该设备可能不是热敏打印机');
}

function disconnect() {
  if (!conn) return;
  wx.closeBLEConnection({ deviceId: conn.deviceId });
  conn = null;
  wx.removeStorageSync('bt_printer');
}

// 分片写入（BLE 单包默认 20 字节）
async function writeBytes(bytes) {
  if (!conn) throw new Error('蓝牙打印机未连接');
  const MTU = 20;
  for (let i = 0; i < bytes.length; i += MTU) {
    const chunk = bytes.slice(i, i + MTU);
    await new Promise((resolve, reject) => {
      wx.writeBLECharacteristicValue({
        deviceId: conn.deviceId,
        serviceId: conn.serviceId,
        characteristicId: conn.charId,
        value: chunk.buffer.slice(chunk.byteOffset, chunk.byteOffset + chunk.length),
        writeType: conn.writeType,
        success: resolve,
        fail: (e) => reject(new Error('写入失败：' + e.errMsg))
      });
    });
    await new Promise((r) => setTimeout(r, 20)); // 给打印机缓冲时间
  }
}

module.exports = { isConnected, currentName, ensureAdapter, scanDevices, connect, disconnect, writeBytes };
