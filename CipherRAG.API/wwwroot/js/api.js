class ApiError extends Error {
  constructor(status, message, data) {
    super(message);
    this.status = status;
    this.data = data;
  }
}

const ACCESS_TOKEN_KEY = 'cipherrag.accessToken';

function getAccessToken() {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

function setAccessToken(token) {
  if (token) localStorage.setItem(ACCESS_TOKEN_KEY, token);
  else localStorage.removeItem(ACCESS_TOKEN_KEY);
}

async function request(path, options = {}) {
  const config = {
    method: options.method || 'GET',
    headers: { ...(options.headers || {}) },
    credentials: 'same-origin'
  };

  const accessToken = getAccessToken();
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }

  if (options.body !== undefined && options.body !== null) {
    if (options.body instanceof FormData) {
      config.body = options.body;
    } else {
      config.headers['Content-Type'] = 'application/json';
      config.body = JSON.stringify(options.body);
    }
  }

  let response;
  try {
    response = await fetch(path, config);
  } catch (e) {
    throw new ApiError(0, 'Network error — could not reach the server.');
  }

  if (response.status === 401) {
    setAccessToken(null);
    window.dispatchEvent(new CustomEvent('app:unauthorized'));
  }

  const contentType = response.headers.get('content-type') || '';
  let data = null;
  if (contentType.includes('application/json')) {
    try { data = await response.json(); } catch { data = null; }
  }

  if (!response.ok) {
    let msg = `Request failed (${response.status})`;
    if (data && data.message) msg = data.message;
    else if (data && data.Message) msg = data.Message;
    else if (data && data.title) msg = data.title;
    throw new ApiError(response.status, msg, data);
  }

  if (response.status === 204) return null;
  return data;
}

export const api = {
  request,
  setAccessToken,
  clearAccessToken: () => setAccessToken(null),

  // --- Auth ---
  login: (email, password) => request('/api/auth/login', {
    method: 'POST',
    body: { email, password }
  }),
  logout: () => request('/api/auth/logout', { method: 'POST' }),
  me: () => request('/api/auth/me'),

  // --- Health ---
  health: () => request('/api/health'),

  // --- RAG ---
  ask: (userPrompt) => request('/api/rag/ask', {
    method: 'POST',
    body: { userPrompt }
  }),

  // --- Documents ---
  getDocuments: () => request('/api/documents'),
  getPagedDocuments: (pageNumber = 1, pageSize = 10) =>
    request(`/api/documents/paged?pageNumber=${pageNumber}&pageSize=${pageSize}`),
  getDocument: (id) => request(`/api/documents/${id}`),
  createDocument: (data) => request('/api/documents', { method: 'POST', body: data }),
  uploadDocument: (formData) => request('/api/documents/upload', {
    method: 'POST',
    body: formData
  }),
  ingestDocument: (id, rawText) => request(`/api/documents/${id}/ingest`, {
    method: 'POST',
    body: { rawText }
  }),
  updateDocument: (id, data) => request(`/api/documents/${id}`, {
    method: 'PUT',
    body: data
  }),
  deleteDocument: (id) => request(`/api/documents/${id}`, { method: 'DELETE' }),

  // --- Audit logs ---
  getAuditLogs: () => request('/api/audit-logs'),
  getPagedAuditLogs: (pageNumber = 1, pageSize = 15) =>
    request(`/api/audit-logs/paged?pageNumber=${pageNumber}&pageSize=${pageSize}`),
  getFlaggedAuditLogs: () => request('/api/audit-logs/flagged'),
  getAuditLog: (id) => request(`/api/audit-logs/${id}`),
  verifyAuditChain: () => request('/api/audit-logs/verify-chain'),

  // --- Users ---
  getUsers: () => request('/api/users'),
  getPagedUsers: (pageNumber = 1, pageSize = 15) =>
    request(`/api/users/paged?pageNumber=${pageNumber}&pageSize=${pageSize}`),
  getUser: (id) => request(`/api/users/${id}`),
  createUser: (data) => request('/api/users', { method: 'POST', body: data }),
  updateUser: (id, data) => request(`/api/users/${id}`, { method: 'PUT', body: data }),
  deleteUser: (id) => request(`/api/users/${id}`, { method: 'DELETE' }),

  // --- Roles ---
  getRoles: () => request('/api/roles'),
  getRole: (id) => request(`/api/roles/${id}`),
  createRole: (data) => request('/api/roles', { method: 'POST', body: data }),
  updateRole: (id, data) => request(`/api/roles/${id}`, { method: 'PUT', body: data }),
  deleteRole: (id) => request(`/api/roles/${id}`, { method: 'DELETE' }),

  // --- ABAC Policies ---
  getPolicies: () => request('/api/policies'),
  getPolicy: (id) => request(`/api/policies/${id}`),
  getPolicyRoles: () => request('/api/policies/roles'),
  createPolicy: (data) => request('/api/policies', { method: 'POST', body: data }),
  updatePolicy: (id, data) => request(`/api/policies/${id}`, { method: 'PUT', body: data }),
  togglePolicy: (id) => request(`/api/policies/${id}/toggle`, { method: 'PATCH' }),
  deletePolicy: (id) => request(`/api/policies/${id}`, { method: 'DELETE' })
};

export { ApiError };
