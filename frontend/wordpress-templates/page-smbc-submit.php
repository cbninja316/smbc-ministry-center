<?php
/**
 * Template Name: SMBC Submit Request
 * Description: SMBC — Public request submission form (no login required)
 */
$smbc = site_url('/wp-content/themes/smbc-admin/smbc');
?><!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>SMBC — Submit a Request</title>
  <?php wp_head(); ?>
  <link rel="stylesheet" href="<?php echo esc_url($smbc); ?>/css/styles.css">
  <style>
    #wpadminbar { display: none !important; }
    html { margin-top: 0 !important; }
  </style>
</head>
<body>

  <nav class="nav">
    <span class="nav-brand">SMBC Ministry Center</span>
  </nav>

  <div class="dashboard">
    <div class="section-card">
      <h2 style="margin-bottom:8px;">Submit a Request</h2>
      <p class="submit-notice">
        Use this form to submit church event requests, facility use requests, benevolence requests,
        maintenance reports, or receipts. All submissions go directly to church staff.
      </p>

      <!-- ── Type selector ─────────────────────────────────────────── -->
      <div class="submit-type-grid">

        <button class="submit-type-btn active" data-type="ChurchEvent">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
          </svg>
          Church Event
        </button>

        <button class="submit-type-btn" data-type="FacilityUse">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/>
          </svg>
          Facility Use
        </button>

        <button class="submit-type-btn" data-type="Benevolence">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
          </svg>
          Benevolence
        </button>

        <button class="submit-type-btn" data-type="Maintenance">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>
          </svg>
          Maintenance
        </button>

        <button class="submit-type-btn" data-type="Receipt">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="9 11 12 14 22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>
          </svg>
          Receipt
        </button>

      </div>

      <!-- ── Church Event Form ─────────────────────────────────────── -->
      <div id="form-ChurchEvent" class="submit-form-section">
        <div class="form-row">
          <div class="field">
            <label>Ministry / Group <span style="color:#dc2626">*</span></label>
            <input type="text" id="ce-ministry" placeholder="e.g. Youth Ministry">
          </div>
          <div class="field">
            <label>Contact Person <span style="color:#dc2626">*</span></label>
            <input type="text" id="ce-contact" placeholder="Your name">
          </div>
        </div>
        <div class="field">
          <label>Event Name <span style="color:#dc2626">*</span></label>
          <input type="text" id="ce-name" placeholder="e.g. Summer Youth Camp">
        </div>
        <div class="form-row">
          <div class="field">
            <label>Event Date(s) <span style="color:#dc2626">*</span></label>
            <input type="date" id="ce-date">
          </div>
          <div class="field">
            <label>Event Time</label>
            <input type="text" id="ce-time" placeholder="e.g. 6:00 PM – 8:00 PM">
          </div>
        </div>
        <div class="form-row">
          <div class="field">
            <label>Location</label>
            <input type="text" id="ce-location" placeholder="e.g. Fellowship Hall">
          </div>
          <div class="field">
            <label>Cost</label>
            <input type="text" id="ce-cost" placeholder="e.g. Free or $5/person">
          </div>
        </div>
        <div class="field">
          <label style="display:flex;align-items:center;gap:8px;font-weight:400;cursor:pointer;">
            <input type="checkbox" id="ce-registration" style="width:16px;height:16px;">
            <span style="font-weight:600;font-size:0.85rem;color:var(--secondary);">Registration Required</span>
          </label>
        </div>
        <div class="field">
          <label>Event Description <span style="color:#dc2626">*</span></label>
          <textarea id="ce-description" rows="4" placeholder="Describe the event, purpose, and any special needs..."></textarea>
        </div>
        <div class="field">
          <label>Promotion Needed</label>
          <div style="display:flex;flex-direction:column;gap:10px;margin-top:6px;">
            <label style="display:flex;align-items:center;gap:8px;font-weight:400;cursor:pointer;">
              <input type="checkbox" id="ce-promo-fb" style="width:16px;height:16px;">Post on Church Facebook Page
            </label>
            <label style="display:flex;align-items:center;gap:8px;font-weight:400;cursor:pointer;">
              <input type="checkbox" id="ce-promo-fb-event" style="width:16px;height:16px;">Create Facebook Event
            </label>
            <label style="display:flex;align-items:center;gap:8px;font-weight:400;cursor:pointer;">
              <input type="checkbox" id="ce-promo-text" style="width:16px;height:16px;">Text Message Alert
            </label>
            <label style="display:flex;align-items:center;gap:8px;font-weight:400;cursor:pointer;">
              <input type="checkbox" id="ce-promo-email" style="width:16px;height:16px;">Email Newsletter
            </label>
          </div>
        </div>
      </div>

      <!-- ── Facility Use Form ─────────────────────────────────────── -->
      <div id="form-FacilityUse" class="submit-form-section hidden">
        <div class="field">
          <label>Event / Activity Name <span style="color:#dc2626">*</span></label>
          <input type="text" id="fu-name" placeholder="e.g. Baby Shower">
        </div>
        <div class="form-row">
          <div class="field">
            <label>Event Date <span style="color:#dc2626">*</span></label>
            <input type="date" id="fu-date">
          </div>
          <div class="field">
            <label>Requested By <span style="color:#dc2626">*</span></label>
            <input type="text" id="fu-requestedby" placeholder="Your name">
          </div>
        </div>
        <div class="field">
          <label>Description <span style="color:#dc2626">*</span></label>
          <textarea id="fu-description" rows="4" placeholder="Describe the event, expected attendance, rooms or equipment needed..."></textarea>
        </div>
      </div>

      <!-- ── Benevolence Form ──────────────────────────────────────── -->
      <div id="form-Benevolence" class="submit-form-section hidden">
        <div class="benev-notice">
          All information provided is kept strictly confidential. Requests are reviewed by the
          Benevolence Committee. Submitting this form does not guarantee assistance will be provided.
        </div>
        <div class="form-row">
          <div class="field">
            <label>Full Name <span style="color:#dc2626">*</span></label>
            <input type="text" id="bn-requestedby" placeholder="Your full name">
          </div>
          <div class="field">
            <label>Phone Number</label>
            <input type="tel" id="bn-phone" placeholder="(405) 555-0100">
          </div>
        </div>
        <div class="field">
          <label>Street Address</label>
          <input type="text" id="bn-street" placeholder="123 Main St">
        </div>
        <div class="form-row">
          <div class="field">
            <label>City</label>
            <input type="text" id="bn-city" placeholder="Moore">
          </div>
          <div class="field" style="display:grid;grid-template-columns:1fr 2fr;gap:0 12px;">
            <div>
              <label>State</label>
              <input type="text" id="bn-state" placeholder="OK" maxlength="2">
            </div>
            <div>
              <label>Zip Code</label>
              <input type="text" id="bn-zip" placeholder="73160">
            </div>
          </div>
        </div>
        <div class="form-row">
          <div class="field">
            <label>Amount Requested</label>
            <input type="number" id="bn-amount" step="0.01" min="0" placeholder="0.00">
          </div>
          <div class="field">
            <label>Date Needed</label>
            <input type="date" id="bn-dateneeded">
          </div>
        </div>
        <div class="field">
          <label>Relationship to Church</label>
          <input type="text" id="bn-relationship" placeholder="e.g. Member, Regular Attendee, Community Member">
        </div>
        <div class="field">
          <label>Reason for Request <span style="color:#dc2626">*</span></label>
          <textarea id="bn-description" rows="4" placeholder="Please describe your need and how the assistance will be used..."></textarea>
        </div>
        <div class="benev-section">
          <p style="font-size:0.82rem;color:var(--text-muted);margin-bottom:14px;line-height:1.5;">
            By signing below, I certify that the information provided is true and accurate to the best of my knowledge.
            I understand that any assistance provided is a gift from the church and is not a loan.
          </p>
          <div class="form-row">
            <div class="field">
              <label>Signature (type full name) <span style="color:#dc2626">*</span></label>
              <input type="text" id="bn-signature" placeholder="Type your full name">
            </div>
            <div class="field">
              <label>Date <span style="color:#dc2626">*</span></label>
              <input type="date" id="bn-sigdate">
            </div>
          </div>
        </div>
      </div>

      <!-- ── Maintenance Form ──────────────────────────────────────── -->
      <div id="form-Maintenance" class="submit-form-section hidden">
        <div class="form-row">
          <div class="field">
            <label>Title / Item <span style="color:#dc2626">*</span></label>
            <input type="text" id="mt-name" placeholder="e.g. Leaking roof in gym">
          </div>
          <div class="field">
            <label>Requested By <span style="color:#dc2626">*</span></label>
            <input type="text" id="mt-requestedby" placeholder="Your name">
          </div>
        </div>
        <div class="form-row">
          <div class="field">
            <label>Date Needed By</label>
            <input type="date" id="mt-date">
          </div>
          <div class="field">
            <label>Urgency</label>
            <select id="mt-urgency">
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="Urgent">Urgent</option>
            </select>
          </div>
        </div>
        <div class="field">
          <label>Details <span style="color:#dc2626">*</span></label>
          <textarea id="mt-description" rows="4" placeholder="Describe the issue, location in the building, and any relevant details..."></textarea>
        </div>
      </div>

      <!-- ── Receipt Form ──────────────────────────────────────────── -->
      <div id="form-Receipt" class="submit-form-section hidden">
        <div class="form-row">
          <div class="field">
            <label>Date <span style="color:#dc2626">*</span></label>
            <input type="date" id="rc-date">
          </div>
          <div class="field">
            <label>Ministry <span style="color:#dc2626">*</span></label>
            <input type="text" id="rc-ministry" placeholder="e.g. Youth">
          </div>
        </div>
        <div class="form-row">
          <div class="field">
            <label>Amount <span style="color:#dc2626">*</span></label>
            <input type="number" id="rc-amount" step="0.01" min="0" placeholder="0.00">
          </div>
          <div class="field">
            <label>Submitted By <span style="color:#dc2626">*</span></label>
            <input type="text" id="rc-submittedby" placeholder="Your name">
          </div>
        </div>
        <div class="field">
          <label>Description <span style="color:#dc2626">*</span></label>
          <textarea id="rc-description" rows="3" placeholder="What was purchased and why?"></textarea>
        </div>
        <div class="field">
          <label>Receipt Image <span style="color:#dc2626">*</span></label>
          <input type="file" id="rc-image" accept="image/jpeg,image/png,image/webp,application/pdf"
            style="border:1.5px solid var(--border) !important;border-radius:var(--radius-sm) !important;padding:8px 12px !important;width:100% !important;font-size:0.9rem;cursor:pointer;background:#fff !important;">
          <div id="rc-preview" style="margin-top:10px;display:none;">
            <img id="rc-preview-img" style="max-width:100%;max-height:200px;border-radius:var(--radius-sm);border:1.5px solid var(--border);">
          </div>
        </div>
      </div>

      <!-- ── Submit button ─────────────────────────────────────────── -->
      <div style="margin-top:32px;">
        <button class="btn btn-primary btn-full" id="submit-btn" style="font-size:1rem;padding:14px;">
          Submit Request
        </button>
      </div>

    </div>
  </div>

  <script src="<?php echo esc_url($smbc); ?>/js/config.js"></script>
  <script src="<?php echo esc_url($smbc); ?>/js/submit.js"></script>
  <script>
    document.getElementById('rc-image').addEventListener('change', function() {
      const file = this.files[0];
      if (!file || file.type === 'application/pdf') return;
      const reader = new FileReader();
      reader.onload = (e) => {
        document.getElementById('rc-preview-img').src = e.target.result;
        document.getElementById('rc-preview').style.display = 'block';
      };
      reader.readAsDataURL(file);
    });

    const today = new Date().toISOString().split('T')[0];
    document.getElementById('bn-sigdate').value = today;
    document.getElementById('rc-date').value = today;
  </script>
  <?php wp_footer(); ?>
</body>
</html>
