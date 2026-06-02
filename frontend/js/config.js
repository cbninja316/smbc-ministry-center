// ─────────────────────────────────────────────────────────────────
//  SMBC Admin — Configuration
//  Change these values when switching between local and production.
// ─────────────────────────────────────────────────────────────────

const SMBC_CONFIG = {
  // LOCAL TESTING:  'http://localhost:5000/api'
  // NGROK TESTING:  'https://YOUR-NGROK-URL.ngrok-free.app/api'
  // PRODUCTION:     point to your church machine's public URL
  API_BASE: 'https://glorious-magical-overboard.ngrok-free.dev/api',

  // LOCAL TESTING (.html files):  use paths below as-is
  // PRODUCTION (WordPress):       swap to WordPress slugs ('/smbc-login/', '/smbc-admin/', etc.)
  PAGES: {
    login:          '/smbc-login/',
    dashboard:      '/smbc-admin/',
    setupPassword:  '/smbc-setup-password/',
    users:          '/smbc-users/',
    submit:         '/smbc-submit/',
  }
};
