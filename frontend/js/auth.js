function _page(key) {
  if (typeof SMBC_CONFIG !== 'undefined') return SMBC_CONFIG.PAGES[key];
  const fallback = { login: '/login.html', dashboard: '/index.html', setupPassword: '/setup-password.html', users: '/users.html' };
  return fallback[key];
}

function getSession() {
  const raw = localStorage.getItem('smbc_session');
  return raw ? JSON.parse(raw) : null;
}

function saveSession(data) {
  localStorage.setItem('smbc_token', data.token);
  localStorage.setItem('smbc_session', JSON.stringify({
    username: data.username,
    role: data.role,
    allowedItemTypes: data.allowedItemTypes
  }));
}

function clearSession() {
  localStorage.removeItem('smbc_token');
  localStorage.removeItem('smbc_session');
}

function requireAuth() {
  if (!getToken()) window.location.href = _page('login');
}

function isSuperAdmin() {
  return getSession()?.role === 'SuperAdmin';
}

function canAccess(type) {
  const session = getSession();
  if (!session) return false;
  if (session.role === 'SuperAdmin') return true;
  return session.allowedItemTypes.includes(type);
}
