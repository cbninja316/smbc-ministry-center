const API_BASE = (typeof SMBC_CONFIG !== 'undefined') ? SMBC_CONFIG.API_BASE : 'http://localhost:5000/api';

function getToken() { return localStorage.getItem('smbc_token'); }

async function apiFetch(path, options = {}) {
  const token = getToken();
  const headers = { 'Content-Type': 'application/json', 'ngrok-skip-browser-warning': 'true', ...options.headers };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });

  if (res.status === 401) {
    if (getToken()) {
      // Existing session expired — clear and redirect to login
      localStorage.clear();
      window.location.href = _page('login');
      return;
    }
    // No token means this is a login attempt with wrong credentials
    const err = await res.json().catch(() => ({ message: 'Invalid username or password.' }));
    throw new Error(err.message || 'Invalid username or password.');
  }

  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'An error occurred.' }));
    throw new Error(err.message || 'An error occurred.');
  }

  if (res.status === 204) return null;
  return res.json();
}

async function apiUpload(path, formData) {
  const token = getToken();
  const headers = { 'ngrok-skip-browser-warning': 'true' };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { method: 'POST', headers, body: formData });

  if (res.status === 401) {
    localStorage.clear();
    window.location.href = _page('login');
    return;
  }

  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Upload failed.' }));
    throw new Error(err.message || 'Upload failed.');
  }

  return res.json();
}

const Auth = {
  login: (username, password) =>
    apiFetch('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) }),
  setPassword: (token, password) =>
    apiFetch('/auth/set-password', { method: 'POST', body: JSON.stringify({ token, password }) }),
  validateInvite: (token) =>
    apiFetch(`/auth/validate-invite?token=${encodeURIComponent(token)}`),
};

const Items = {
  getAll: () => apiFetch('/items'),
  getSummary: () => apiFetch('/items/summary'),
  create: (data) => apiFetch('/items', { method: 'POST', body: JSON.stringify(data) }),
  update: (id, data) => apiFetch(`/items/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  delete: (id) => apiFetch(`/items/${id}`, { method: 'DELETE' }),
  reorder: (updates) => apiFetch('/items/reorder', { method: 'PATCH', body: JSON.stringify(updates) }),
};

const Receipts = {
  getAll: () => apiFetch('/receipts'),
  create: (formData) => apiUpload('/receipts', formData),
  markDone: (id) => apiFetch(`/receipts/${id}/done`, { method: 'PATCH' }),
  delete: (id) => apiFetch(`/receipts/${id}`, { method: 'DELETE' }),
  imageUrl: (id) => `${API_BASE}/receipts/${id}/image`,
};

const Users = {
  getAll: () => apiFetch('/users'),
  invite: (data) => apiFetch('/users/invite', { method: 'POST', body: JSON.stringify(data) }),
  updatePermissions: (id, types) => apiFetch(`/users/${id}/permissions`, { method: 'PUT', body: JSON.stringify(types) }),
  delete: (id) => apiFetch(`/users/${id}`, { method: 'DELETE' }),
};
