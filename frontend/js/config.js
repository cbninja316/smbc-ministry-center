// ─────────────────────────────────────────────────────────────────
//  SMBC Admin — Configuration
//  Change these values when switching between local and production.
// ─────────────────────────────────────────────────────────────────

const SMBC_CONFIG = {
  // LOCAL TESTING:  'http://localhost:5000/api'
  // NGROK TESTING:  'https://YOUR-NGROK-URL.ngrok-free.app/api'
  // PRODUCTION:     point to your church machine's public URL
  API_BASE: 'http://localhost:5000/api',

  // LOCAL TESTING (.html files):  use paths below as-is
  // PRODUCTION (WordPress):       swap to WordPress slugs ('/smbc-login/', '/smbc-admin/', etc.)
  PAGES: {
    login:          '/login.html',
    dashboard:      '/index.html',
    setupPassword:  '/setup-password.html',
    users:          '/users.html',
    submit:         '/submit.html',
  }
};
