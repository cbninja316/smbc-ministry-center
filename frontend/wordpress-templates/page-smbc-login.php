<?php
/**
 * Template Name: SMBC Login
 * Description: SMBC Admin — Login page
 */
$smbc = site_url('/wp-content/themes/smbc-admin/smbc');
?><!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>SMBC Admin — Login</title>
  <?php wp_head(); ?>
  <link rel="stylesheet" href="<?php echo esc_url($smbc); ?>/css/styles.css">
  <style>
    #wpadminbar { display: none !important; }
    html { margin-top: 0 !important; }
  </style>
</head>
<body class="auth-page">
  <div class="auth-card">
    <h1>SMBC Admin</h1>
    <p>Sign in to access the status board.</p>
    <div id="alert-container"></div>
    <div class="field">
      <label for="username">Username</label>
      <input type="text" id="username" autocomplete="username" placeholder="Enter your username">
    </div>
    <div class="field">
      <label for="password">Password</label>
      <input type="password" id="password" autocomplete="current-password" placeholder="Enter your password">
    </div>
    <button class="btn btn-primary btn-full" id="login-btn">Sign In</button>
  </div>

  <script src="<?php echo esc_url($smbc); ?>/js/config.js"></script>
  <script src="<?php echo esc_url($smbc); ?>/js/api.js"></script>
  <script src="<?php echo esc_url($smbc); ?>/js/auth.js"></script>
  <script>
    if (getToken()) window.location.href = _page('dashboard');

    const alertContainer = document.getElementById('alert-container');
    const loginBtn = document.getElementById('login-btn');

    function showError(msg) {
      alertContainer.innerHTML = `<div class="alert alert-error">${msg}</div>`;
    }

    async function doLogin() {
      const username = document.getElementById('username').value.trim();
      const password = document.getElementById('password').value;
      if (!username || !password) { showError('Please enter your username and password.'); return; }

      loginBtn.disabled = true;
      loginBtn.textContent = 'Signing in…';

      try {
        const data = await Auth.login(username, password);
        saveSession(data);
        window.location.href = _page('dashboard');
      } catch (e) {
        showError(e.message);
        loginBtn.disabled = false;
        loginBtn.textContent = 'Sign In';
      }
    }

    loginBtn.addEventListener('click', doLogin);
    document.addEventListener('keydown', (e) => { if (e.key === 'Enter') doLogin(); });
  </script>
  <?php wp_footer(); ?>
</body>
</html>
