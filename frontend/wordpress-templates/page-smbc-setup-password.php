<?php
/**
 * Template Name: SMBC Setup Password
 * Description: One Accord — Set password for invited users
 */
$smbc = site_url('/wp-content/themes/smbc-admin/smbc');
?><!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>One Accord — Set Password</title>
  <?php wp_head(); ?>
  <link rel="stylesheet" href="<?php echo esc_url($smbc); ?>/css/styles.css">
  <style>
    #wpadminbar { display: none !important; }
    html { margin-top: 0 !important; }
  </style>
</head>
<body class="auth-page">
  <div class="auth-card">
    <h1>Set Your Password</h1>
    <p id="welcome-msg">Welcome! Create a password to activate your account.</p>
    <div id="alert-container"></div>
    <div id="form-section">
      <div class="field">
        <label for="password">Password</label>
        <input type="password" id="password" autocomplete="new-password" placeholder="Create a password">
      </div>
      <div class="field">
        <label for="confirm">Confirm Password</label>
        <input type="password" id="confirm" autocomplete="new-password" placeholder="Confirm your password">
      </div>
      <button class="btn btn-primary btn-full" id="submit-btn">Activate Account</button>
    </div>
    <div id="invalid-section" class="hidden">
      <p style="color:var(--urgency-urgent-text)">This invite link is invalid or has expired. Please contact your administrator.</p>
    </div>
  </div>

  <script src="<?php echo esc_url($smbc); ?>/js/config.js"></script>
  <script src="<?php echo esc_url($smbc); ?>/js/api.js"></script>
  <script src="<?php echo esc_url($smbc); ?>/js/auth.js"></script>
  <script>
    const params = new URLSearchParams(window.location.search);
    const token = params.get('token');
    const alertContainer = document.getElementById('alert-container');
    const submitBtn = document.getElementById('submit-btn');

    function showError(msg) {
      alertContainer.innerHTML = `<div class="alert alert-error">${msg}</div>`;
    }

    async function init() {
      if (!token) {
        document.getElementById('form-section').classList.add('hidden');
        document.getElementById('invalid-section').classList.remove('hidden');
        return;
      }
      try {
        const data = await Auth.validateInvite(token);
        document.getElementById('welcome-msg').textContent =
          `Welcome, ${data.username}! Create a password to activate your account.`;
      } catch {
        document.getElementById('form-section').classList.add('hidden');
        document.getElementById('invalid-section').classList.remove('hidden');
      }
    }

    async function doSetPassword() {
      const password = document.getElementById('password').value;
      const confirm = document.getElementById('confirm').value;

      if (password.length < 8) { showError('Password must be at least 8 characters.'); return; }
      if (password !== confirm) { showError('Passwords do not match.'); return; }

      submitBtn.disabled = true;
      submitBtn.textContent = 'Activating…';

      try {
        const data = await Auth.setPassword(token, password);
        saveSession(data);
        window.location.href = _page('dashboard');
      } catch (e) {
        showError(e.message);
        submitBtn.disabled = false;
        submitBtn.textContent = 'Activate Account';
      }
    }

    submitBtn.addEventListener('click', doSetPassword);
    document.addEventListener('keydown', (e) => { if (e.key === 'Enter') doSetPassword(); });
    init();
  </script>
  <?php wp_footer(); ?>
</body>
</html>
