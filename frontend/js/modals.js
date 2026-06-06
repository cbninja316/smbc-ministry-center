function openModal(html, wide = false) {
  const overlay = document.createElement("div");
  overlay.className = "modal-overlay";
  overlay.innerHTML = `<div class="modal${wide ? " modal-wide" : ""}">${html}</div>`;
  overlay.addEventListener("click", (e) => {
    if (e.target === overlay) closeModal(overlay);
  });
  document.body.appendChild(overlay);
  return overlay;
}

function closeModal(overlay) {
  overlay.remove();
}

function showAlert(container, message, type = "error") {
  const existing = container.querySelector(".alert");
  if (existing) existing.remove();
  const el = document.createElement("div");
  el.className = `alert alert-${type}`;
  el.textContent = message;
  container.prepend(el);
}

// ── Generic item forms ────────────────────────────────────────────────────────

const ITEM_FIELDS = {
  ChurchEvent: [
    "name",
    "eventDate",
    "ministry",
    "urgency",
    "requestedBy",
    "description",
  ],
  FacilityUse: ["name", "eventDate", "requestedBy", "description"],
  Maintenance: ["name", "eventDate", "urgency", "requestedBy", "description"],
  SecretaryRequest: ["requestedBy", "email", "description"],
};

const FIELD_LABELS = {
  name: "Event Name",
  eventDate: "Date",
  ministry: "Ministry",
  urgency: "Urgency",
  requestedBy: "Requested By",
  email: "Email",
  description: "Description",
};

function buildItemForm(type, item = null) {
  const fields = ITEM_FIELDS[type] || [];
  return fields
    .map((f) => {
      const val = item ? (item[f] ?? "") : "";
      if (f === "urgency") {
        return `
        <div class="field">
          <label>${FIELD_LABELS[f]}</label>
          <select name="urgency">
            <option value="">-- Select --</option>
            ${["Low", "Medium", "Urgent"]
              .map(
                (u) =>
                  `<option value="${u}" ${val === u ? "selected" : ""}>${u}</option>`,
              )
              .join("")}
          </select>
        </div>`;
      }
      if (f === "description") {
        return `
        <div class="field">
          <label>${FIELD_LABELS[f]}</label>
          <textarea name="description" rows="4">${val}</textarea>
        </div>`;
      }
      if (f === "eventDate") {
        const dateVal = val ? new Date(val).toISOString().split("T")[0] : "";
        return `
        <div class="field">
          <label>${FIELD_LABELS[f]}</label>
          <input type="date" name="eventDate" value="${dateVal}">
        </div>`;
      }
      if (f === "email") {
        return `
        <div class="field">
          <label>${FIELD_LABELS[f]}</label>
          <input type="email" name="email" value="${escModalHtml(val)}" placeholder="email@example.com">
        </div>`;
      }
      return `
      <div class="field">
        <label>${FIELD_LABELS[f]}</label>
        <input type="text" name="${f}" value="${escModalHtml(val)}">
      </div>`;
    })
    .join("");
}

function getFormData(form) {
  const data = {};
  new FormData(form).forEach((v, k) => {
    data[k] = v;
  });
  return data;
}

// ── Benevolence form ─────────────────────────────────────────────────────────

function buildBenevolenceForm(item = null) {
  const b = item?.benevolenceDetails || {};
  const v = (key, fallback = "") => escModalHtml(b[key] ?? fallback);
  const dateVal = (key) => (b[key] ? b[key].split("T")[0] : "");
  const det = b.determination || "";
  const method = b.methodOfAssistance || "";
  const showApproval = det === "ApprovedFull" || det === "ApprovedPart";

  return `
    <p class="benev-notice">When a church assists church members or other individuals, the IRS requires the church to keep certain documentation and records. This form should be filled out each time a request for financial assistance is received and/or the church helps a person financially. This confidential form should be kept with the church's financial records.</p>

    <div class="field">
      <label>Name of Applicant</label>
      <input type="text" id="b-applicant" value="${item ? escModalHtml(item.requestedBy) : ""}">
    </div>
    <div class="field">
      <label>Street Address</label>
      <input type="text" id="b-street" value="${v("streetAddress")}">
    </div>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
      <div class="field">
        <label>City</label>
        <input type="text" id="b-city" value="${v("city")}">
      </div>
      <div class="field">
        <label>State</label>
        <input type="text" id="b-state" value="${v("state")}">
      </div>
    </div>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
      <div class="field">
        <label>Zip Code</label>
        <input type="text" id="b-zip" value="${v("zipCode")}">
      </div>
      <div class="field">
        <label>Phone</label>
        <input type="tel" id="b-phone" value="${v("phone")}">
      </div>
    </div>
    <div class="field">
      <label>Brief description of assistance being requested</label>
      <textarea id="b-description" rows="3">${item ? escModalHtml(item.description) : ""}</textarea>
    </div>
    <div class="field">
      <label>Amount of assistance requested, if known</label>
      <input type="number" id="b-amount-req" step="1.00" min="0" value="${v("amountRequested")}">
    </div>
    <div class="field">
      <label>Date assistance is needed by, if applicable</label>
      <input type="date" id="b-date-needed" value="${dateVal("dateNeeded")}">
    </div>

    <div class="benev-section">
      <p style="font-weight:700;margin-bottom:6px;">REQUESTOR ACKNOWLEDGMENT</p>
      <p style="font-size:0.88rem;color:var(--text-muted);margin-bottom:14px;">I understand that submission of this request does not guarantee financial assistance. I certify that the information I have provided is true and complete to the best of my knowledge.</p>
      <div style="display:grid;grid-template-columns:2fr 1fr;gap:12px;">
        <div class="field">
          <label>Applicant/Requestor Signature</label>
          <input type="text" id="b-signature" placeholder="Type full name as signature" value="${v("applicantSignature")}">
        </div>
        <div class="field">
          <label>Date</label>
          <input type="date" id="b-sig-date" value="${dateVal("signatureDate")}">
        </div>
      </div>
    </div>

    <div class="benev-committee-section">
      <p class="benev-committee-header">FOR BENEVOLENCE COMMITTEE USE ONLY</p>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
        <div class="field">
          <label>Date Reviewed</label>
          <input type="date" id="b-date-reviewed" value="${dateVal("dateReviewed")}">
        </div>
        <div class="field">
          <label>Relationship to church members or church leaders</label>
          <input type="text" id="b-relationship" value="${v("relationshipToChurch")}">
        </div>
      </div>

      <p style="font-weight:700;margin:16px 0 10px;">DETERMINATION</p>
      <div style="display:flex;flex-direction:column;gap:8px;margin-bottom:14px;">
        <label class="benev-radio-label">
          <input type="radio" name="b-determination" value="ApprovedFull" ${det === "ApprovedFull" ? "checked" : ""}>
          ☐ Request Approved in Full
        </label>
        <label class="benev-radio-label">
          <input type="radio" name="b-determination" value="ApprovedPart" ${det === "ApprovedPart" ? "checked" : ""}>
          ☐ Request Approved in Part
        </label>
        <label class="benev-radio-label">
          <input type="radio" name="b-determination" value="NotApproved" ${det === "NotApproved" ? "checked" : ""}>
          ☐ Request Not Approved
        </label>
      </div>

      <div class="field">
        <label>Reason the assistance was granted or the request was not fulfilled</label>
        <textarea id="b-denial-reason" rows="2">${v("denialReason")}</textarea>
      </div>

      <div id="b-approval-details" style="${showApproval ? "" : "display:none;"}">
        <div class="field">
          <label>Brief description of assistance provided by the church</label>
          <textarea id="b-assist-desc" rows="2">${v("assistanceProvidedDescription")}</textarea>
        </div>
        <div class="field">
          <label>Cost of the assistance</label>
          <input type="number" id="b-assist-cost" step="1.00" min="0" value="${v("assistanceCost")}">
        </div>

        <p style="font-weight:600;margin:12px 0 8px;">Method of Assistance:</p>
        <div style="display:flex;flex-direction:column;gap:8px;margin-bottom:14px;">
          <label class="benev-radio-label">
            <input type="radio" name="b-method" value="DirectPayment" ${method === "DirectPayment" ? "checked" : ""}>
            ☐ Payment Directly to Provider/Business
          </label>
          <label class="benev-radio-label">
            <input type="radio" name="b-method" value="CashGrant" ${method === "CashGrant" ? "checked" : ""}>
            ☐ Cash Grant
          </label>
          <label class="benev-radio-label">
            <input type="radio" name="b-method" value="Other" ${method === "Other" ? "checked" : ""}>
            ☐ Other:
            <input type="text" id="b-method-other" placeholder="describe" style="margin-left:8px;flex:1;border:none;border-bottom:1px solid var(--border);outline:none;font-size:0.9rem;" value="${v("methodOtherDescription")}">
          </label>
        </div>

        <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
          <div class="field">
            <label>Payable To/Provider Name</label>
            <input type="text" id="b-payable-to" value="${v("payableTo")}">
          </div>
          <div class="field">
            <label>Date Assistance Provided</label>
            <input type="date" id="b-date-provided" value="${dateVal("dateAssistanceProvided")}">
          </div>
        </div>
      </div>
    </div>

    <div class="benev-guidelines">
      <p><strong>BENEVOLENCE GUIDELINES</strong></p>
      <p>Applicants for financial assistance are awarded financial assistance based on financial need. Applicants are not granted financial assistance based on relationships between the applicant and church leaders or significant church contributors.</p>
      <p>The church does not discriminate applicants based upon race, color, sex, national origin, age, geographic territory, or disability. However, the church reserves the right to discriminate based on religion.</p>
      <p>The church benevolence committee may provide short-term emergency assistance and longer-term aid to ensure that individuals have basic necessities such as food, clothing, housing, transportation, and medical assistance, including psychological counseling.</p>
      <p><strong>CONFIDENTIAL RECORDS NOTICE</strong></p>
      <p>This document contains confidential financial assistance information and should be retained with the church's financial records.</p>
    </div>
  `;
}

function setupDeterminationToggle(overlay) {
  overlay.querySelectorAll('input[name="b-determination"]').forEach((radio) => {
    radio.addEventListener("change", () => {
      const val = overlay.querySelector(
        'input[name="b-determination"]:checked',
      )?.value;
      const details = overlay.querySelector("#b-approval-details");
      if (details)
        details.style.display =
          val === "ApprovedFull" || val === "ApprovedPart" ? "" : "none";
    });
  });
}

function collectBenevolenceData(overlay) {
  return {
    streetAddress: overlay.querySelector("#b-street")?.value || null,
    city: overlay.querySelector("#b-city")?.value || null,
    state: overlay.querySelector("#b-state")?.value || null,
    zipCode: overlay.querySelector("#b-zip")?.value || null,
    phone: overlay.querySelector("#b-phone")?.value || null,
    amountRequested:
      parseFloat(overlay.querySelector("#b-amount-req")?.value) || null,
    dateNeeded: overlay.querySelector("#b-date-needed")?.value || null,
    applicantSignature: overlay.querySelector("#b-signature")?.value || null,
    signatureDate: overlay.querySelector("#b-sig-date")?.value || null,
    dateReviewed: overlay.querySelector("#b-date-reviewed")?.value || null,
    relationshipToChurch:
      overlay.querySelector("#b-relationship")?.value || null,
    determination:
      overlay.querySelector('input[name="b-determination"]:checked')?.value ||
      null,
    denialReason: overlay.querySelector("#b-denial-reason")?.value || null,
    assistanceProvidedDescription:
      overlay.querySelector("#b-assist-desc")?.value || null,
    assistanceCost:
      parseFloat(overlay.querySelector("#b-assist-cost")?.value) || null,
    methodOfAssistance:
      overlay.querySelector('input[name="b-method"]:checked')?.value || null,
    methodOtherDescription:
      overlay.querySelector("#b-method-other")?.value || null,
    payableTo: overlay.querySelector("#b-payable-to")?.value || null,
    dateAssistanceProvided:
      overlay.querySelector("#b-date-provided")?.value || null,
  };
}

// ── Church Event form ─────────────────────────────────────────────────────────

function buildChurchEventForm(item = null) {
  const c = item?.churchEventDetails || {};
  const v = (key, fallback = "") => escModalHtml(c[key] ?? fallback);
  const chk = (key) => (c[key] ? "checked" : "");
  const nameVal = escModalHtml(item?.name ?? "");
  const ministryVal = escModalHtml(item?.ministry ?? "");
  const contactVal = escModalHtml(item?.requestedBy ?? "");
  const dateVal = item?.eventDate
    ? new Date(item.eventDate).toISOString().split("T")[0]
    : "";
  const descVal = escModalHtml(item?.description ?? "");

  return `
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
      <div class="field">
        <label>Ministry/Group</label>
        <input type="text" id="ce-ministry" value="${ministryVal}">
      </div>
      <div class="field">
        <label>Contact Person</label>
        <input type="text" id="ce-contact" value="${contactVal}">
      </div>
    </div>
    <div class="field">
      <label>Event Name</label>
      <input type="text" id="ce-name" value="${nameVal}">
    </div>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
      <div class="field">
        <label>Event Date(s)</label>
        <input type="date" id="ce-date" value="${dateVal}">
      </div>
      <div class="field">
        <label>Event Time</label>
        <input type="time" id="ce-time" value="${v("eventTime")}">
      </div>
    </div>
    <div class="field">
      <label>Location</label>
      <input type="text" id="ce-location" value="${v("location")}">
    </div>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
      <div class="field">
        <label>Cost (if any)</label>
        <input type="number" id="ce-cost" step="1.00" min="0" placeholder="0.00" value="${v("cost")}">
      </div>
      <div class="field" style="display:flex;flex-direction:column;justify-content:flex-end;">
        <label style="margin-bottom:10px;">Registration</label>
        <div style="display:flex;gap:20px;">
          <label class="benev-radio-label">
            <input type="radio" name="ce-registration" value="yes" ${c.registrationRequired ? "checked" : ""}> Yes
          </label>
          <label class="benev-radio-label">
            <input type="radio" name="ce-registration" value="no" ${!c.registrationRequired ? "checked" : ""}> Not Required
          </label>
        </div>
      </div>
    </div>
    <div class="field">
      <label>Event Description</label>
      <textarea id="ce-description" rows="4" placeholder="Overview of the event and any registration information...">${descVal}</textarea>
    </div>

    <div class="benev-committee-section" style="border-color:var(--primary);background:#f0f4f9;">
      <p style="font-weight:700;font-size:0.95rem;color:var(--primary);margin-bottom:14px;letter-spacing:0.03em;">PROMOTION &amp; COMMUNICATION REQUESTS</p>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px;">
        <label class="benev-radio-label" style="align-items:flex-start;gap:10px;">
          <input type="checkbox" id="ce-fb-post" ${chk("promoteFacebook")} style="margin-top:2px;">
          <span><strong>Facebook Post</strong><br><span style="font-size:0.8rem;color:var(--text-muted);">Standard post on church page for regular followers.</span></span>
        </label>
        <label class="benev-radio-label" style="align-items:flex-start;gap:10px;">
          <input type="checkbox" id="ce-fb-event" ${chk("promoteFacebookEvent")} style="margin-top:2px;">
          <span><strong>Facebook Event</strong><br><span style="font-size:0.8rem;color:var(--text-muted);">Dedicated event page — discoverable and shareable beyond regular followers.</span></span>
        </label>
        <label class="benev-radio-label" style="align-items:flex-start;gap:10px;">
          <input type="checkbox" id="ce-text" ${chk("promoteText")} style="margin-top:2px;">
          <span><strong>Text Message</strong><br><span style="font-size:0.8rem;color:var(--text-muted);">Sent to text message subscribers only.</span></span>
        </label>
        <label class="benev-radio-label" style="align-items:flex-start;gap:10px;">
          <input type="checkbox" id="ce-email" ${chk("promoteEmail")} style="margin-top:2px;">
          <span><strong>Email Message</strong><br><span style="font-size:0.8rem;color:var(--text-muted);">Sent to email subscribers only.</span></span>
        </label>
      </div>
    </div>

    <div class="benev-guidelines">
      <p><strong>MINISTRY EVENT GUIDELINES</strong></p>
      <p><strong>Promotion Requests</strong><br>The earlier your request is received, the better we can promote your event effectively.</p>
      <p><strong>Complete Details</strong><br>Please provide all details needed for promotion and planning.</p>
      <p><strong>Shared Responsibility</strong><br>Requesting ministry is responsible for setup, cleanup, and budget planning for event. Please turn in all receipts for event in a timely manner.</p>
      <p><strong>Changes or Cancellations</strong><br>Please notify the church office as soon as possible if your event changes or is canceled.</p>
      <p><em>Thank you for planning ahead! Your early communication helps us support your ministry well.</em></p>
    </div>
  `;
}

function collectChurchEventData(overlay) {
  return {
    eventTime: overlay.querySelector("#ce-time")?.value || null,
    location: overlay.querySelector("#ce-location")?.value || null,
    cost: parseFloat(overlay.querySelector("#ce-cost")?.value) || null,
    registrationRequired:
      overlay.querySelector('input[name="ce-registration"]:checked')?.value ===
      "yes",
    promoteFacebook: overlay.querySelector("#ce-fb-post")?.checked || false,
    promoteFacebookEvent:
      overlay.querySelector("#ce-fb-event")?.checked || false,
    promoteText: overlay.querySelector("#ce-text")?.checked || false,
    promoteEmail: overlay.querySelector("#ce-email")?.checked || false,
  };
}

function openChurchEventModal(item, onSave, onDelete) {
  const isEdit = item !== null;
  const overlay = openModal(
    `
    <div class="modal-header">
      <h2>${isEdit ? "Edit Church Event" : "New Church Event"}</h2>
      <button class="modal-close" onclick="this.closest('.modal-overlay').remove()">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
      </button>
    </div>
    ${buildChurchEventForm(item)}
    <div class="modal-actions" style="margin-top:24px;">
      ${isEdit && onDelete ? '<button class="btn btn-danger" id="delete-btn">Delete</button>' : ""}
      <button class="btn" id="print-btn" style="background:#f0f4f9;color:var(--secondary);margin-right:auto;">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-right:4px;"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
        Print
      </button>
      <button class="btn btn-primary" id="save-btn">${isEdit ? "Save" : "Create"}</button>
    </div>
  `,
    true,
  );

  overlay.querySelector("#print-btn").addEventListener("click", () => {
    const name = overlay.querySelector("#ce-name")?.value.trim() || "";
    const ministry = overlay.querySelector("#ce-ministry")?.value.trim() || "";
    const contact = overlay.querySelector("#ce-contact")?.value.trim() || "";
    const date = overlay.querySelector("#ce-date")?.value || "";
    const description =
      overlay.querySelector("#ce-description")?.value.trim() || "";
    const data = collectChurchEventData(overlay);
    printChurchEventForm(name, ministry, contact, date, description, data);
  });

  overlay.querySelector("#save-btn").addEventListener("click", async () => {
    const name = overlay.querySelector("#ce-name")?.value.trim() || "";
    if (!name) {
      showAlert(overlay.querySelector(".modal"), "Event Name is required.");
      return;
    }
    const payload = {
      type: "ChurchEvent",
      name,
      eventDate: overlay.querySelector("#ce-date")?.value || null,
      ministry: overlay.querySelector("#ce-ministry")?.value.trim() || null,
      urgency: null,
      requestedBy: overlay.querySelector("#ce-contact")?.value.trim() || "",
      description: overlay.querySelector("#ce-description")?.value.trim() || "",
      churchEventData: collectChurchEventData(overlay),
    };
    try {
      await onSave(payload);
      closeModal(overlay);
    } catch (e) {
      showAlert(overlay.querySelector(".modal"), e.message);
    }
  });

  overlay.querySelector("#delete-btn")?.addEventListener("click", async () => {
    if (!confirm("Delete this event?")) return;
    try {
      await onDelete();
      closeModal(overlay);
    } catch (e) {
      showAlert(overlay.querySelector(".modal"), e.message);
    }
  });
}

// ── Edit / create modals ──────────────────────────────────────────────────────

function openEditModal(item, onSave, onDelete) {
  if (item.type === "Benevolence") {
    openBenevolenceModal(item, onSave, onDelete);
    return;
  }
  if (item.type === "ChurchEvent") {
    openChurchEventModal(item, onSave, onDelete);
    return;
  }

  const typeLabel =
    {
      ChurchEvent: "Church Event",
      FacilityUse: "Facility Use",
      Maintenance: "Maintenance Request",
      SecretaryRequest: "Secretary Request",
    }[item.type] || item.type;

  const overlay = openModal(`
    <div class="modal-header">
      <h2>Edit ${escModalHtml(item.name || typeLabel)}</h2>
      <button class="modal-close" onclick="this.closest('.modal-overlay').remove()">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
      </button>
    </div>
    <form id="edit-form">
      ${buildItemForm(item.type, item)}
    </form>
    <div class="modal-actions">
      <button class="btn btn-danger" id="delete-btn">Delete</button>
      <button class="btn btn-primary" id="save-btn">Save</button>
    </div>
  `);

  overlay.querySelector("#save-btn").addEventListener("click", async () => {
    const form = overlay.querySelector("#edit-form");
    const raw = getFormData(form);
    const payload = {
      type: item.type,
      name: raw.name || "",
      eventDate: raw.eventDate || null,
      ministry: raw.ministry || null,
      urgency: raw.urgency || null,
      requestedBy: raw.requestedBy || "",
      email: raw.email || null,
      description: raw.description || "",
    };
    try {
      await onSave(payload);
      closeModal(overlay);
    } catch (e) {
      showAlert(overlay.querySelector(".modal"), e.message);
    }
  });

  overlay.querySelector("#delete-btn").addEventListener("click", async () => {
    if (!confirm("Delete this item?")) return;
    try {
      await onDelete();
      closeModal(overlay);
    } catch (e) {
      showAlert(overlay.querySelector(".modal"), e.message);
    }
  });
}

function openCreateModal(type, onSave) {
  if (type === "Benevolence") {
    openBenevolenceModal(null, onSave, null);
    return;
  }
  if (type === "ChurchEvent") {
    openChurchEventModal(null, onSave, null);
    return;
  }

  const typeLabel =
    {
      ChurchEvent: "Church Event",
      FacilityUse: "Facility Use",
      Maintenance: "Maintenance Request",
      SecretaryRequest: "Secretary Request",
    }[type] || type;

  const overlay = openModal(`
    <div class="modal-header">
      <h2>New ${typeLabel}</h2>
      <button class="modal-close" onclick="this.closest('.modal-overlay').remove()">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
      </button>
    </div>
    <form id="create-form">
      ${buildItemForm(type)}
    </form>
    <div class="modal-actions">
      <button class="btn btn-primary" id="create-btn">Create</button>
    </div>
  `);

  overlay.querySelector("#create-btn").addEventListener("click", async () => {
    const form = overlay.querySelector("#create-form");
    const raw = getFormData(form);
    const payload = {
      type,
      name: raw.name || "",
      eventDate: raw.eventDate || null,
      ministry: raw.ministry || null,
      urgency: raw.urgency || null,
      requestedBy: raw.requestedBy || "",
      email: raw.email || null,
      description: raw.description || "",
    };
    try {
      await onSave(payload);
      closeModal(overlay);
    } catch (e) {
      showAlert(overlay.querySelector(".modal"), e.message);
    }
  });
}

function openBenevolenceModal(item, onSave, onDelete) {
  const isEdit = item !== null;
  const overlay = openModal(
    `
    <div class="modal-header">
      <h2>${isEdit ? "Edit Benevolence Request" : "New Benevolence Request"}</h2>
      <button class="modal-close" onclick="this.closest('.modal-overlay').remove()">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
      </button>
    </div>
    ${buildBenevolenceForm(item)}
    <div class="modal-actions" style="margin-top:24px;">
      ${isEdit && onDelete ? '<button class="btn btn-danger" id="delete-btn">Delete</button>' : ""}
      <button class="btn" id="print-btn" style="background:#f0f4f9;color:var(--secondary);margin-right:auto;">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-right:4px;"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
        Print
      </button>
      <button class="btn btn-primary" id="save-btn">${isEdit ? "Save" : "Create"}</button>
    </div>
  `,
    true,
  );

  setupDeterminationToggle(overlay);

  overlay.querySelector("#save-btn").addEventListener("click", async () => {
    const applicant = overlay.querySelector("#b-applicant")?.value.trim() || "";
    const description =
      overlay.querySelector("#b-description")?.value.trim() || "";

    if (!applicant) {
      showAlert(
        overlay.querySelector(".modal"),
        "Name of Applicant is required.",
      );
      return;
    }

    const payload = {
      type: "Benevolence",
      name: applicant,
      eventDate: null,
      ministry: null,
      urgency: null,
      requestedBy: applicant,
      description,
      benevolenceData: collectBenevolenceData(overlay),
    };

    try {
      await onSave(payload);
      closeModal(overlay);
    } catch (e) {
      showAlert(overlay.querySelector(".modal"), e.message);
    }
  });

  overlay.querySelector("#print-btn").addEventListener("click", () => {
    const applicant = overlay.querySelector("#b-applicant")?.value.trim() || "";
    const description =
      overlay.querySelector("#b-description")?.value.trim() || "";
    const data = collectBenevolenceData(overlay);
    printBenevolenceForm(applicant, description, data);
  });

  overlay.querySelector("#delete-btn")?.addEventListener("click", async () => {
    if (!confirm("Delete this benevolence request?")) return;
    try {
      await onDelete();
      closeModal(overlay);
    } catch (e) {
      showAlert(overlay.querySelector(".modal"), e.message);
    }
  });
}

// ── Receipt image modal ───────────────────────────────────────────────────────

function openReceiptModal(receiptId) {
  const overlay = openModal(`
    <div class="modal-header">
      <h2>View Receipt</h2>
      <button class="modal-close" onclick="this.closest('.modal-overlay').remove()">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
      </button>
    </div>
    <div class="receipt-image-box" id="receipt-image-container">
      <p style="color:var(--text-muted)">Loading...</p>
    </div>
  `);

  const container = overlay.querySelector("#receipt-image-container");

  fetch(Receipts.imageUrl(receiptId), {
    headers: {
      Authorization: `Bearer ${getToken()}`,
      "ngrok-skip-browser-warning": "true",
    },
  })
    .then(async (res) => {
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const contentType = res.headers.get("Content-Type") || "";
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      if (contentType.includes("pdf")) {
        container.innerHTML = `<iframe src="${url}"></iframe>`;
      } else {
        container.innerHTML = `<img src="${url}" alt="Receipt">`;
      }
    })
    .catch(() => {
      container.innerHTML = `<p style="color:var(--urgency-urgent-text)">Failed to load receipt image.</p>`;
    });
}

// ── Print church event form ───────────────────────────────────────────────────

function printChurchEventForm(
  name,
  ministry,
  contact,
  date,
  description,
  data,
) {
  const e = (str) =>
    String(str ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
  const fmtDate = (val) =>
    val ? new Date(val + "T00:00:00").toLocaleDateString("en-US") : "";
  const fmtTime = (val) => {
    if (!val) return "";
    const [h, m] = val.split(":");
    const hour = parseInt(h);
    return `${hour % 12 || 12}:${m} ${hour < 12 ? "AM" : "PM"}`;
  };
  const fmtMoney = (val) => (val ? `$${parseFloat(val).toFixed(2)}` : "");
  const check = (condition) => (condition ? "☑" : "☐");

  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Ministry Event Request — ${e(name)}</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: Arial, sans-serif; font-size: 11pt; color: #000; padding: 24px 32px; }
    h1 { font-size: 15pt; text-align: center; margin-bottom: 16px; }
    .row { display: flex; gap: 16px; margin-bottom: 10px; }
    .field { margin-bottom: 10px; flex: 1; }
    .field label { font-size: 8.5pt; font-weight: bold; display: block; margin-bottom: 2px; color: #333; }
    .field .value { border-bottom: 1px solid #000; min-height: 18px; padding: 1px 2px; font-size: 10.5pt; }
    .field .value.multiline { border: 1px solid #000; min-height: 60px; padding: 4px; }
    .section { border: 1.5px solid #005DBA; padding: 10px 12px; margin: 12px 0; background: #f7faff; }
    .section-title { font-weight: bold; font-size: 10pt; color: #005DBA; margin-bottom: 10px; letter-spacing: 0.04em; }
    .promo-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
    .promo-item { font-size: 10pt; }
    .guidelines { font-size: 8pt; color: #444; border-top: 1px solid #ccc; margin-top: 16px; padding-top: 10px; line-height: 1.5; }
    .guidelines p { margin-bottom: 6px; }
    @media print { body { padding: 0; } @page { margin: 18mm 16mm; } }
  </style>
</head>
<body>
  <h1>Ministry Event Request</h1>

  <div class="row">
    <div class="field"><label>Ministry/Group</label><div class="value">${e(ministry)}</div></div>
    <div class="field"><label>Contact Person</label><div class="value">${e(contact)}</div></div>
  </div>
  <div class="field"><label>Event Name</label><div class="value">${e(name)}</div></div>
  <div class="row">
    <div class="field"><label>Event Date(s)</label><div class="value">${fmtDate(date)}</div></div>
    <div class="field"><label>Event Time</label><div class="value">${fmtTime(data.eventTime)}</div></div>
  </div>
  <div class="field"><label>Location</label><div class="value">${e(data.location)}</div></div>
  <div class="row">
    <div class="field"><label>Cost (if any)</label><div class="value">${fmtMoney(data.cost)}</div></div>
    <div class="field">
      <label>Registration</label>
      <div class="value">${check(data.registrationRequired)} Yes &nbsp;&nbsp; ${check(!data.registrationRequired)} Not Required</div>
    </div>
  </div>
  <div class="field">
    <label>Event Description</label>
    <div class="value multiline">${e(description)}</div>
  </div>

  <div class="section">
    <div class="section-title">PROMOTION &amp; COMMUNICATION REQUESTS</div>
    <div class="promo-grid">
      <div class="promo-item">${check(data.promoteFacebook)} <strong>Facebook Post</strong><br><span style="font-size:8pt;color:#555;">Standard post on church page</span></div>
      <div class="promo-item">${check(data.promoteFacebookEvent)} <strong>Facebook Event</strong><br><span style="font-size:8pt;color:#555;">Dedicated event page</span></div>
      <div class="promo-item">${check(data.promoteText)} <strong>Text Message</strong><br><span style="font-size:8pt;color:#555;">Text subscribers only</span></div>
      <div class="promo-item">${check(data.promoteEmail)} <strong>Email Message</strong><br><span style="font-size:8pt;color:#555;">Email subscribers only</span></div>
    </div>
  </div>

  <div class="guidelines">
    <p><strong>MINISTRY EVENT GUIDELINES</strong></p>
    <p><strong>Promotion Requests</strong> — The earlier your request is received, the better we can promote your event effectively.</p>
    <p><strong>Complete Details</strong> — Please provide all details needed for promotion and planning.</p>
    <p><strong>Shared Responsibility</strong> — Requesting ministry is responsible for setup, cleanup, and budget planning for event. Please turn in all receipts for event in a timely manner.</p>
    <p><strong>Changes or Cancellations</strong> — Please notify the church office as soon as possible if your event changes or is canceled.</p>
    <p><em>Thank you for planning ahead! Your early communication helps us support your ministry well.</em></p>
  </div>

  <script>window.onload = () => { window.print(); }<\/script>
</body>
</html>`;

  const win = window.open("", "_blank");
  win.document.write(html);
  win.document.close();
}

// ── Print benevolence form ────────────────────────────────────────────────────

function printBenevolenceForm(applicant, description, data) {
  const e = (str) =>
    String(str ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
  const fmt = (val) => (val ? e(val) : "");
  const fmtDate = (val) =>
    val ? new Date(val + "T00:00:00").toLocaleDateString("en-US") : "";
  const fmtMoney = (val) => (val ? `$${parseFloat(val).toFixed(2)}` : "");
  const check = (condition) => (condition ? "☑" : "☐");

  const det = data.determination || "";
  const method = data.methodOfAssistance || "";
  const showApproval = det === "ApprovedFull" || det === "ApprovedPart";

  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Benevolence Request — ${e(applicant)}</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: Arial, sans-serif; font-size: 11pt; color: #000; padding: 24px 32px; }
    h1 { font-size: 15pt; text-align: center; margin-bottom: 4px; }
    .subtitle { text-align: center; font-size: 9pt; color: #555; margin-bottom: 16px; }
    .notice { font-size: 9pt; color: #444; border-left: 3px solid #005DBA; padding: 8px 10px; margin-bottom: 16px; line-height: 1.5; }
    .row { display: flex; gap: 16px; margin-bottom: 10px; }
    .field { margin-bottom: 10px; flex: 1; }
    .field label { font-size: 8.5pt; font-weight: bold; display: block; margin-bottom: 2px; color: #333; }
    .field .value { border-bottom: 1px solid #000; min-height: 18px; padding: 1px 2px; font-size: 10.5pt; }
    .field .value.multiline { border: 1px solid #000; min-height: 40px; padding: 4px; }
    .section { border: 1px solid #000; padding: 10px 12px; margin: 12px 0; }
    .section-title { font-weight: bold; font-size: 10pt; margin-bottom: 8px; }
    .committee-section { border: 1.5px solid #000; padding: 10px 12px; margin: 12px 0; background: #fafafa; }
    .committee-header { font-weight: bold; font-size: 10pt; text-align: center; border-bottom: 1px solid #000; padding-bottom: 6px; margin-bottom: 10px; letter-spacing: 0.05em; }
    .determination { margin: 8px 0; }
    .determination div { margin: 4px 0; font-size: 10.5pt; }
    .guidelines { font-size: 8pt; color: #444; border-top: 1px solid #ccc; margin-top: 16px; padding-top: 10px; line-height: 1.5; }
    .guidelines p { margin-bottom: 6px; }
    .sig-row { display: flex; gap: 24px; margin-top: 8px; }
    .sig-line { flex: 2; border-bottom: 1px solid #000; min-height: 20px; }
    .date-line { flex: 1; border-bottom: 1px solid #000; min-height: 20px; }
    @media print {
      body { padding: 0; }
      @page { margin: 18mm 16mm; }
    }
  </style>
</head>
<body>
  <h1>Benevolence Assistance Request</h1>
  <div class="subtitle">CONFIDENTIAL — Retain with church financial records</div>

  <div class="notice">When a church assists church members or other individuals, the IRS requires the church to keep certain documentation and records. This form should be filled out each time a request for financial assistance is received and/or the church helps a person financially.</div>

  <div class="field">
    <label>Name of Applicant</label>
    <div class="value">${fmt(applicant)}</div>
  </div>
  <div class="field">
    <label>Street Address</label>
    <div class="value">${fmt(data.streetAddress)}</div>
  </div>
  <div class="row">
    <div class="field"><label>City</label><div class="value">${fmt(data.city)}</div></div>
    <div class="field" style="flex:0.4"><label>State</label><div class="value">${fmt(data.state)}</div></div>
    <div class="field" style="flex:0.6"><label>Zip Code</label><div class="value">${fmt(data.zipCode)}</div></div>
    <div class="field"><label>Phone</label><div class="value">${fmt(data.phone)}</div></div>
  </div>
  <div class="field">
    <label>Brief description of assistance being requested</label>
    <div class="value multiline">${fmt(description)}</div>
  </div>
  <div class="row">
    <div class="field"><label>Amount of assistance requested, if known</label><div class="value">${fmtMoney(data.amountRequested)}</div></div>
    <div class="field"><label>Date assistance is needed by, if applicable</label><div class="value">${fmtDate(data.dateNeeded)}</div></div>
  </div>

  <div class="section">
    <div class="section-title">REQUESTOR ACKNOWLEDGMENT</div>
    <p style="font-size:9pt;color:#444;margin-bottom:10px;">I understand that submission of this request does not guarantee financial assistance. I certify that the information I have provided is true and complete to the best of my knowledge.</p>
    <div class="row">
      <div style="flex:2">
        <label style="font-size:8.5pt;font-weight:bold;">Applicant/Requestor Signature</label>
        <div class="sig-line" style="margin-top:16px;">${fmt(data.applicantSignature)}</div>
      </div>
      <div style="flex:1">
        <label style="font-size:8.5pt;font-weight:bold;">Date</label>
        <div class="date-line" style="margin-top:16px;">${fmtDate(data.signatureDate)}</div>
      </div>
    </div>
  </div>

  <div class="committee-section">
    <div class="committee-header">FOR BENEVOLENCE COMMITTEE USE ONLY</div>
    <div class="row">
      <div class="field"><label>Date Reviewed</label><div class="value">${fmtDate(data.dateReviewed)}</div></div>
      <div class="field"><label>Relationship to church members or church leaders</label><div class="value">${fmt(data.relationshipToChurch)}</div></div>
    </div>

    <div style="font-weight:bold;margin:10px 0 6px;">DETERMINATION</div>
    <div class="determination">
      <div>${check(det === "ApprovedFull")} Request Approved in Full</div>
      <div>${check(det === "ApprovedPart")} Request Approved in Part</div>
      <div>${check(det === "NotApproved")} Request Not Approved</div>
    </div>

    <div class="field" style="margin-top:10px;">
      <label>Reason the assistance was granted or the request was not fulfilled</label>
      <div class="value multiline">${fmt(data.denialReason)}</div>
    </div>

    ${
      showApproval
        ? `
    <div class="field">
      <label>Brief description of assistance provided by the church</label>
      <div class="value multiline">${fmt(data.assistanceProvidedDescription)}</div>
    </div>
    <div class="row">
      <div class="field"><label>Cost of the assistance</label><div class="value">${fmtMoney(data.assistanceCost)}</div></div>
    </div>
    <div style="font-weight:bold;margin:8px 0 4px;font-size:10pt;">Method of Assistance:</div>
    <div class="determination">
      <div>${check(method === "DirectPayment")} Payment Directly to Provider/Business</div>
      <div>${check(method === "CashGrant")} Cash Grant</div>
      <div style="display:flex;gap:8px;align-items:baseline;">${check(method === "Other")} Other: <span style="border-bottom:1px solid #000;flex:1;min-width:120px;">${method === "Other" ? fmt(data.methodOtherDescription) : ""}</span></div>
    </div>
    <div class="row" style="margin-top:10px;">
      <div class="field"><label>Payable To/Provider Name</label><div class="value">${fmt(data.payableTo)}</div></div>
      <div class="field"><label>Date Assistance Provided</label><div class="value">${fmtDate(data.dateAssistanceProvided)}</div></div>
    </div>
    `
        : ""
    }
  </div>

  <div class="guidelines">
    <p><strong>BENEVOLENCE GUIDELINES</strong></p>
    <p>Applicants for financial assistance are awarded financial assistance based on financial need. Applicants are not granted financial assistance based on relationships between the applicant and church leaders or significant church contributors.</p>
    <p>The church does not discriminate applicants based upon race, color, sex, national origin, age, geographic territory, or disability. However, the church reserves the right to discriminate based on religion.</p>
    <p>The church benevolence committee may provide short-term emergency assistance and longer-term aid to ensure that individuals have basic necessities such as food, clothing, housing, transportation, and medical assistance, including psychological counseling.</p>
    <p><strong>CONFIDENTIAL RECORDS NOTICE</strong></p>
    <p>This document contains confidential financial assistance information and should be retained with the church's financial records.</p>
  </div>

  <script>window.onload = () => { window.print(); }<\/script>
</body>
</html>`;

  const win = window.open("", "_blank");
  win.document.write(html);
  win.document.close();
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function escModalHtml(str) {
  return String(str ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}
