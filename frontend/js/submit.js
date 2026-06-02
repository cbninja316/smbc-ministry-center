const API_BASE = (typeof SMBC_CONFIG !== 'undefined') ? SMBC_CONFIG.API_BASE : 'http://localhost:5000/api';

let currentType = 'ChurchEvent';

// ── Type selector ─────────────────────────────────────────────────────────────

document.querySelectorAll('.submit-type-btn').forEach(btn => {
  btn.addEventListener('click', () => selectType(btn.dataset.type));
});

function selectType(type) {
  currentType = type;
  document.querySelectorAll('.submit-type-btn').forEach(b =>
    b.classList.toggle('active', b.dataset.type === type));
  document.querySelectorAll('.submit-form-section').forEach(s =>
    s.classList.add('hidden'));
  document.getElementById(`form-${type}`).classList.remove('hidden');
}

// ── Submit ────────────────────────────────────────────────────────────────────

document.getElementById('submit-btn').addEventListener('click', handleSubmit);

async function handleSubmit() {
  const btn = document.getElementById('submit-btn');
  btn.disabled = true;
  btn.textContent = 'Submitting…';

  try {
    if (currentType === 'Receipt') {
      await submitReceipt();
    } else {
      await submitItem();
    }
    showToast('Your request has been submitted successfully!', 'success');
    resetForm(currentType);
  } catch (e) {
    showToast(e.message || 'Something went wrong. Please try again.', 'error');
  } finally {
    btn.disabled = false;
    btn.textContent = 'Submit Request';
  }
}

// ── Item submission ───────────────────────────────────────────────────────────

async function submitItem() {
  const payload = buildItemPayload(currentType);

  const res = await fetch(`${API_BASE}/public/items`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'ngrok-skip-browser-warning': 'true',
    },
    body: JSON.stringify(payload),
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.message || 'Submission failed.');
  }
}

function buildItemPayload(type) {
  switch (type) {
    case 'ChurchEvent': return buildChurchEventPayload();
    case 'FacilityUse': return buildFacilityUsePayload();
    case 'Benevolence': return buildBenevolencePayload();
    case 'Maintenance': return buildMaintenancePayload();
    default: throw new Error('Unknown type.');
  }
}

function val(id) {
  return document.getElementById(id)?.value?.trim() ?? '';
}
function checked(id) {
  return document.getElementById(id)?.checked ?? false;
}
function require(value, label) {
  if (!value) throw new Error(`${label} is required.`);
  return value;
}

function buildChurchEventPayload() {
  const ministry    = require(val('ce-ministry'), 'Ministry / Group');
  const contact     = require(val('ce-contact'),  'Contact Person');
  const name        = require(val('ce-name'),      'Event Name');
  const date        = require(val('ce-date'),      'Event Date');
  const description = require(val('ce-description'), 'Event Description');

  return {
    type: 'ChurchEvent',
    name,
    ministry,
    requestedBy: contact,
    eventDate: date,
    description,
    churchEventData: {
      eventTime:            val('ce-time')     || null,
      location:             val('ce-location') || null,
      cost:                 val('ce-cost')     || null,
      registrationRequired: checked('ce-registration'),
      promoteFacebook:      checked('ce-promo-fb'),
      promoteFacebookEvent: checked('ce-promo-fb-event'),
      promoteText:          checked('ce-promo-text'),
      promoteEmail:         checked('ce-promo-email'),
    },
  };
}

function buildFacilityUsePayload() {
  return {
    type: 'FacilityUse',
    name:        require(val('fu-name'),        'Event Name'),
    requestedBy: require(val('fu-requestedby'), 'Requested By'),
    eventDate:   require(val('fu-date'),        'Event Date') || null,
    description: require(val('fu-description'), 'Description'),
  };
}

function buildBenevolencePayload() {
  const requestedBy = require(val('bn-requestedby'), 'Full Name');
  const description = require(val('bn-description'), 'Reason for Request');
  const signature   = require(val('bn-signature'),   'Applicant Signature');
  const sigDate     = require(val('bn-sigdate'),     'Signature Date');

  return {
    type: 'Benevolence',
    name: requestedBy,
    requestedBy,
    description,
    benevolenceData: {
      streetAddress:    val('bn-street')       || null,
      city:             val('bn-city')         || null,
      state:            val('bn-state')        || null,
      zipCode:          val('bn-zip')          || null,
      phone:            val('bn-phone')        || null,
      amountRequested:  val('bn-amount')       ? parseFloat(val('bn-amount')) : null,
      dateNeeded:       val('bn-dateneeded')   || null,
      relationshipToChurch: val('bn-relationship') || null,
      applicantSignature: signature,
      signatureDate:    sigDate,
    },
  };
}

function buildMaintenancePayload() {
  return {
    type: 'Maintenance',
    name:        require(val('mt-name'),        'Title'),
    requestedBy: require(val('mt-requestedby'), 'Requested By'),
    eventDate:   val('mt-date') || null,
    urgency:     val('mt-urgency') || 'Low',
    description: require(val('mt-description'), 'Details'),
  };
}

// ── Receipt submission ────────────────────────────────────────────────────────

async function submitReceipt() {
  const date        = require(val('rc-date'),        'Date');
  const ministry    = require(val('rc-ministry'),    'Ministry');
  const description = require(val('rc-description'), 'Description');
  const amount      = require(val('rc-amount'),      'Amount');
  const submittedBy = require(val('rc-submittedby'), 'Submitted By');
  const imageFile   = document.getElementById('rc-image')?.files[0];

  if (!imageFile) throw new Error('Receipt image is required.');

  const formData = new FormData();
  formData.append('date', date);
  formData.append('ministry', ministry);
  formData.append('description', description);
  formData.append('amount', amount);
  formData.append('submittedBy', submittedBy);
  formData.append('image', imageFile);

  const res = await fetch(`${API_BASE}/public/receipts`, {
    method: 'POST',
    headers: { 'ngrok-skip-browser-warning': 'true' },
    body: formData,
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.message || 'Receipt submission failed.');
  }
}

// ── Reset form ────────────────────────────────────────────────────────────────

function resetForm(type) {
  const section = document.getElementById(`form-${type}`);
  section.querySelectorAll('input:not([type=checkbox]):not([type=radio]), textarea, select').forEach(el => {
    el.value = el.type === 'date' ? '' : '';
  });
  section.querySelectorAll('input[type=checkbox]').forEach(el => el.checked = false);
  // Reset receipt preview
  if (type === 'Receipt') {
    const preview = document.getElementById('rc-preview');
    if (preview) { preview.style.display = 'none'; }
  }
}

// ── Toast ─────────────────────────────────────────────────────────────────────

function showToast(message, type = 'success') {
  const toast = document.createElement('div');
  toast.className = `toast toast-${type}`;
  toast.textContent = message;
  document.body.appendChild(toast);
  setTimeout(() => {
    toast.classList.add('toast-out');
    setTimeout(() => toast.remove(), 300);
  }, 4500);
}
