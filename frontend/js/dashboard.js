let allItems = [];
let allReceipts = [];
const PREVIEW_LIMIT = 2;

async function loadDashboard() {
  try {
    const [items, receipts, summary] = await Promise.all([
      Items.getAll(),
      canAccess('Receipt') ? Receipts.getAll() : Promise.resolve([]),
      Items.getSummary(),
    ]);

    allItems = items || [];
    allReceipts = receipts || [];

    renderSummary(summary);
    renderBoard();
    renderReceipts();
  } catch (e) {
    console.error('Failed to load dashboard:', e);
  }
}

// ── Summary ──────────────────────────────────────────────────────────────────

function renderSummary(summary) {
  document.getElementById('stat-events').textContent = summary.events ?? 0;
  document.getElementById('stat-requests').textContent = summary.benevolence ?? 0;
  document.getElementById('stat-facility').textContent = summary.facilityUses ?? 0;
  document.getElementById('stat-maintenance').textContent = summary.maintenance ?? 0;

  const secretaryCard = document.getElementById('stat-card-secretary');
  if (secretaryCard) {
    if (canAccess('SecretaryRequest')) {
      secretaryCard.classList.remove('hidden');
      document.getElementById('stat-secretary').textContent = summary.secretaryRequests ?? 0;
    } else {
      secretaryCard.classList.add('hidden');
    }
  }
}

function refreshSummary() {
  renderSummary({
    events:             allItems.filter(i => i.type === 'ChurchEvent').length,
    facilityUses:       allItems.filter(i => i.type === 'FacilityUse').length,
    benevolence:        allItems.filter(i => i.type === 'Benevolence').length,
    maintenance:        allItems.filter(i => i.type === 'Maintenance').length,
    secretaryRequests:  allItems.filter(i => i.type === 'SecretaryRequest').length,
  });
}

// ── Board ─────────────────────────────────────────────────────────────────────

const COLUMNS = ['ToDo', 'InProgress', 'Done'];
const COL_LABELS = { ToDo: 'To Do', InProgress: 'In Progress', Done: 'Done' };

function itemsByStatus(status) {
  return allItems.filter(i => i.status === status).sort((a, b) => a.sortOrder - b.sortOrder);
}

function urgencyClass(u) {
  if (!u) return '';
  return { Low: 'urgency-low', Medium: 'urgency-medium', Urgent: 'urgency-urgent' }[u] || '';
}

function formatDate(d) {
  if (!d) return '';
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}

function typeLabel(type) {
  return {
    ChurchEvent:      'Event',
    FacilityUse:      'Facility Use',
    Benevolence:      'Benevolence',
    Maintenance:      'Maintenance',
    SecretaryRequest: 'Secretary Request',
  }[type] || type;
}

function renderBoard() {
  COLUMNS.forEach(status => {
    const items = itemsByStatus(status);
    const preview = items.slice(0, PREVIEW_LIMIT);
    const rest = items.length - PREVIEW_LIMIT;

    const col = document.getElementById(`col-${status}`);
    col.querySelector('.kanban-badge').textContent = items.length;

    const container = col.querySelector('.kanban-items');
    container.innerHTML = '';

    preview.forEach(item => {
      container.appendChild(buildItemCard(item));
    });

    const viewMore = col.querySelector('.view-more');
    if (rest > 0) {
      viewMore.textContent = `View More (${rest})`;
      viewMore.classList.remove('hidden');
      viewMore.onclick = () => renderColumnFull(status);
    } else {
      viewMore.classList.add('hidden');
    }
  });
}

function renderColumnFull(status) {
  const items = itemsByStatus(status);
  const col = document.getElementById(`col-${status}`);
  const container = col.querySelector('.kanban-items');
  container.innerHTML = '';
  items.forEach(item => container.appendChild(buildItemCard(item)));
  const viewMore = col.querySelector('.view-more');
  viewMore.textContent = 'Show Less';
  viewMore.onclick = renderBoard;
}

function buildItemCard(item) {
  const card = document.createElement('div');
  card.className = 'item-card';
  card.dataset.id = item.id;
  card.draggable = true;

  const ministry = item.ministry ? ` - ${item.ministry}` : '';
  const submitted = `Submitted ${formatDate(item.submittedAt)}`;

  card.innerHTML = `
    <div class="item-card-header">
      <span class="item-name">${escHtml(item.name || typeLabel(item.type))}</span>
      ${item.urgency ? `<span class="urgency-badge ${urgencyClass(item.urgency)}">${item.urgency}</span>` : ''}
    </div>
    <div class="item-meta">${typeLabel(item.type)}${escHtml(ministry)}</div>
    <div class="item-meta">${submitted}</div>
  `;

  card.querySelector('.item-name').addEventListener('click', () => {
    openEditModal(
      item,
      (payload) => Items.update(item.id, payload).then(updated => {
        const idx = allItems.findIndex(i => i.id === item.id);
        if (idx !== -1) allItems[idx] = updated;
        renderBoard();
      }),
      () => Items.delete(item.id).then(() => {
        allItems = allItems.filter(i => i.id !== item.id);
        renderBoard();
        refreshSummary();
      })
    );
  });

  setupDrag(card, item);
  return card;
}

// ── Drag and drop ─────────────────────────────────────────────────────────────

let dragItem = null;

function setupDrag(card, item) {
  card.addEventListener('dragstart', () => {
    dragItem = item;
    card.classList.add('dragging');
    setTimeout(() => card.classList.add('dragging'), 0);
  });

  card.addEventListener('dragend', () => {
    card.classList.remove('dragging');
    dragItem = null;
    document.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));
  });
}

COLUMNS.forEach(status => {
  const dropZone = document.getElementById(`col-${status}`);

  dropZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.querySelector('.kanban-items').classList.add('drag-over');
  });

  dropZone.addEventListener('dragleave', () => {
    dropZone.querySelector('.kanban-items').classList.remove('drag-over');
  });

  dropZone.addEventListener('drop', async (e) => {
    e.preventDefault();
    dropZone.querySelector('.kanban-items').classList.remove('drag-over');
    if (!dragItem || dragItem.status === status) return;

    const updates = allItems
      .filter(i => i.status === status)
      .map((i, idx) => ({ id: i.id, status, sortOrder: idx + 1 }));

    updates.push({ id: dragItem.id, status, sortOrder: updates.length });

    dragItem.status = status;
    dragItem.sortOrder = updates.length - 1;

    renderBoard();

    try {
      await Items.reorder(updates);
    } catch (e) {
      console.error('Reorder failed:', e);
    }
  });
});

// ── Receipts ──────────────────────────────────────────────────────────────────

function renderReceipts() {
  const section = document.getElementById('receipts-section');
  if (!canAccess('Receipt')) { section.classList.add('hidden'); return; }

  const active = allReceipts.filter(r => !r.isDone);
  const done   = allReceipts.filter(r => r.isDone);

  // ── Active receipts (all, no pagination) ──
  const tbody = document.getElementById('receipts-body');
  tbody.innerHTML = '';
  active.forEach(r => tbody.appendChild(buildReceiptRow(r, false)));
  document.getElementById('receipts-view-more').classList.add('hidden');

  // ── Done receipts section ──
  const doneSection    = document.getElementById('receipts-done-section');
  const doneTbody      = document.getElementById('receipts-done-body');
  const doneViewMore   = document.getElementById('receipts-done-view-more');
  doneTbody.innerHTML  = '';

  if (done.length > 0) {
    doneSection.classList.remove('hidden');
    const showAll = doneSection.dataset.expanded === 'true';
    const visible = showAll ? done : done.slice(0, 2);
    visible.forEach(r => doneTbody.appendChild(buildReceiptRow(r, true)));

    if (done.length > 2) {
      doneViewMore.classList.remove('hidden');
      doneViewMore.textContent = showAll ? 'Show Less' : `View More (${done.length - 2} more)`;
      doneViewMore.onclick = () => {
        doneSection.dataset.expanded = showAll ? 'false' : 'true';
        renderReceipts();
      };
    } else {
      doneViewMore.classList.add('hidden');
    }
  } else {
    doneSection.classList.add('hidden');
  }
}

function buildReceiptRow(r, isDone) {
  const tr = document.createElement('tr');
  if (isDone) tr.style.opacity = '0.6';

  tr.innerHTML = `
    <td>${new Date(r.date).toLocaleDateString('en-US')}</td>
    <td>${escHtml(r.ministry)}</td>
    <td>${escHtml(r.description)}</td>
    <td>$${parseFloat(r.amount).toFixed(2)}</td>
    <td>${escHtml(r.submittedBy)}</td>
    <td style="display:flex;gap:8px;align-items:center;justify-content:flex-end;">
      <button class="receipt-view-link">View</button>
      ${!isDone ? `<button class="receipt-done-btn">Done</button>` : ''}
      ${isDone ? `<button class="receipt-delete-btn">Delete</button>` : ''}
    </td>
  `;

  tr.querySelector('.receipt-view-link').addEventListener('click', () => openReceiptModal(r.id));

  if (!isDone) {
    tr.querySelector('.receipt-done-btn').addEventListener('click', async () => {
      try {
        await Receipts.markDone(r.id);
        const idx = allReceipts.findIndex(rec => rec.id === r.id);
        if (idx !== -1) allReceipts[idx].isDone = true;
        renderReceipts();
      } catch (e) {
        console.error('Failed to mark receipt done:', e);
      }
    });
  }

  if (isDone) {
    tr.querySelector('.receipt-delete-btn').addEventListener('click', async () => {
      if (!confirm('Delete this receipt and its image permanently?')) return;
      try {
        await Receipts.delete(r.id);
        allReceipts = allReceipts.filter(rec => rec.id !== r.id);
        renderReceipts();
      } catch (e) {
        console.error('Failed to delete receipt:', e);
      }
    });
  }

  return tr;
}

// ── Add receipt modal ─────────────────────────────────────────────────────────

function openCreateReceiptModal() {
  const overlay = openModal(`
    <div class="modal-header">
      <h2>Add Receipt</h2>
      <button class="modal-close" onclick="this.closest('.modal-overlay').remove()">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
      </button>
    </div>
    <div class="field">
      <label>Date</label>
      <input type="date" id="r-date" value="${new Date().toISOString().split('T')[0]}">
    </div>
    <div class="field">
      <label>Ministry</label>
      <input type="text" id="r-ministry" placeholder="e.g. Youth">
    </div>
    <div class="field">
      <label>Description</label>
      <textarea id="r-description" rows="3" placeholder="What was purchased?"></textarea>
    </div>
    <div class="field">
      <label>Amount</label>
      <input type="number" id="r-amount" step="0.01" min="0" placeholder="0.00">
    </div>
    <div class="field">
      <label>Submitted By</label>
      <input type="text" id="r-submitted-by" placeholder="e.g. John D.">
    </div>
    <div class="field">
      <label>Receipt Image</label>
      <input type="file" id="r-image" accept="image/jpeg,image/png,image/webp,application/pdf"
        style="border:1.5px solid var(--border);border-radius:var(--radius-sm);padding:8px 12px;width:100%;font-size:0.9rem;cursor:pointer;">
      <div id="r-preview" style="margin-top:10px;display:none;">
        <img id="r-preview-img" style="max-width:100%;max-height:200px;border-radius:var(--radius-sm);border:1.5px solid var(--border);">
      </div>
    </div>
    <div class="modal-actions">
      <button class="btn btn-primary" id="r-submit-btn">Save Receipt</button>
    </div>
  `);

  overlay.querySelector('#r-image').addEventListener('change', function() {
    const file = this.files[0];
    if (!file || file.type === 'application/pdf') return;
    const reader = new FileReader();
    reader.onload = (e) => {
      overlay.querySelector('#r-preview-img').src = e.target.result;
      overlay.querySelector('#r-preview').style.display = 'block';
    };
    reader.readAsDataURL(file);
  });

  overlay.querySelector('#r-submit-btn').addEventListener('click', async () => {
    const date = overlay.querySelector('#r-date').value;
    const ministry = overlay.querySelector('#r-ministry').value.trim();
    const description = overlay.querySelector('#r-description').value.trim();
    const amount = overlay.querySelector('#r-amount').value;
    const submittedBy = overlay.querySelector('#r-submitted-by').value.trim();
    const imageFile = overlay.querySelector('#r-image').files[0];

    if (!date || !ministry || !description || !amount || !submittedBy) {
      showAlert(overlay.querySelector('.modal'), 'All fields are required.');
      return;
    }
    if (!imageFile) {
      showAlert(overlay.querySelector('.modal'), 'Please attach a receipt image.');
      return;
    }

    const formData = new FormData();
    formData.append('date', date);
    formData.append('ministry', ministry);
    formData.append('description', description);
    formData.append('amount', amount);
    formData.append('submittedBy', submittedBy);
    formData.append('image', imageFile);

    const btn = overlay.querySelector('#r-submit-btn');
    btn.disabled = true;
    btn.textContent = 'Saving…';

    try {
      const newReceipt = await Receipts.create(formData);
      allReceipts.unshift(newReceipt);
      renderReceipts();
      closeModal(overlay);
    } catch (e) {
      showAlert(overlay.querySelector('.modal'), e.message);
      btn.disabled = false;
      btn.textContent = 'Save Receipt';
    }
  });
}

// ── Add item dropdown ──────────────────────────────────────────────────────────

function setupAddDropdown() {
  const btn = document.getElementById('add-item-btn');
  const menu = document.getElementById('add-item-menu');
  if (!btn) return;

  btn.addEventListener('click', (e) => {
    e.stopPropagation();
    menu.classList.toggle('hidden');
  });

  document.addEventListener('click', () => menu.classList.add('hidden'));

  menu.querySelectorAll('[data-type]').forEach(el => {
    el.addEventListener('click', () => {
      const type = el.dataset.type;
      menu.classList.add('hidden');
      openCreateModal(type, (payload) =>
        Items.create(payload).then(newItem => {
          allItems.push(newItem);
          renderBoard();
          refreshSummary();
        })
      );
    });
  });
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function escHtml(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

// ── Init ──────────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
  requireAuth();

  const session = getSession();
  const usernameEl = document.getElementById('nav-username');
  const logoutBtn = document.getElementById('logout-btn');
  const usersLink = document.getElementById('users-link');
  const navBrand = document.getElementById('nav-brand');

  if (navBrand) navBrand.href = _page('dashboard');
  if (usernameEl) usernameEl.textContent = session?.username || '';
  if (logoutBtn) logoutBtn.addEventListener('click', () => {
    clearSession();
    window.location.href = _page('login');
  });
  if (usersLink) {
    usersLink.href = _page('users');
    if (isSuperAdmin()) usersLink.classList.remove('hidden');
  }

  setupAddDropdown();

  const addReceiptBtn = document.getElementById('add-receipt-btn');
  if (addReceiptBtn) addReceiptBtn.addEventListener('click', openCreateReceiptModal);

  loadDashboard();

  setInterval(loadDashboard, 60_000);
});
