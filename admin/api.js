// 后端地址：开发环境指向本地 API；部署时改为实际域名
const API_BASE = 'https://localhost:5000';

function apiToken() { return localStorage.getItem('admin_token') || ''; }

async function api(url, method = 'GET', body) {
  const resp = await fetch(API_BASE + url, {
    method,
    headers: {
      'Content-Type': 'application/json',
      Authorization: 'Bearer ' + apiToken()
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (resp.status === 401) {
    localStorage.removeItem('admin_token');
    location.reload();
    throw new Error('登录已过期');
  }
  const data = resp.status === 204 ? null : await resp.json().catch(() => null);
  if (!resp.ok) throw new Error((data && data.message) || ('请求失败 ' + resp.status));
  return data;
}

const apiGet = (url) => api(url);
const apiPost = (url, body) => api(url, 'POST', body ?? {});
const apiPut = (url, body) => api(url, 'PUT', body ?? {});
const apiPatch = (url, body) => api(url, 'PATCH', body ?? {});
const apiDel = (url) => api(url, 'DELETE');

/**
 * 文件上传（火山引擎 TOS）
 * ------------------------------------------------------------
 * 与 api() 的三点关键差异，改动前务必看清：
 *   1. body 用 FormData，不能 JSON.stringify —— 文件是二进制
 *   2. 【绝对不要手动设置 Content-Type】
 *      浏览器会自动补 multipart/form-data; boundary=----xxx
 *      手写会丢 boundary，后端 IFormFile 恒为 null
 *   3. 只带 Authorization，其它头交给浏览器
 *
 * @param {string} url     如 /api/admin/upload/image
 * @param {File}   file    来自 el-upload 的 options.file 或 input.files[0]
 * @param {string} folder  对象存储目录，如 products / banners
 * @returns {Promise<{url:string, key:string}>}
 */
async function apiUpload(url, file, folder) {
  const fd = new FormData();
  fd.append('file', file); // 字段名必须叫 file，与后端 IFormFile file 对应

  const qs = folder ? '?folder=' + encodeURIComponent(folder) : '';
  const resp = await fetch(API_BASE + url + qs, {
    method: 'POST',
    headers: {
      Authorization: 'Bearer ' + apiToken()
      // 注意：这里故意不写 Content-Type
    },
    body: fd
  });

  if (resp.status === 401) {
    localStorage.removeItem('admin_token');
    location.reload();
    throw new Error('登录已过期');
  }

  const data = await resp.json().catch(() => null);
  if (!resp.ok) throw new Error((data && data.message) || ('上传失败 ' + resp.status));
  return data;
}
