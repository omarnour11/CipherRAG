import { api } from '../api.js';
import {
  escapeHtml, icon, toast, setLoading
} from '../utils.js';

let policies = [];
let roles = [];

export function renderPoliciesView(container, setTopbar) {
  setTopbar('Security Policies (ABAC)');
  container.innerHTML = `
    <div class="toolbar">
      <div class="toolbar-left">
        <div class="search-box">
          ${icon('search', 14)}
          <input type="text" id="policySearch" placeholder="Search policies by role or department…">
        </div>
        <button class="btn btn-ghost btn-sm" id="policyRefreshBtn">${icon('refresh', 13)} Refresh</button>
      </div>
      <div class="toolbar-right">
        <button class="btn btn-primary" id="policyAddBtn">${icon('plus', 14)} Add Policy</button>
      </div>
    </div>

    <div class="view-pad">
      <div class="stats-grid" id="policyStats"></div>

      <div class="card">
        <div class="card-header">
          <div class="card-title">${icon('shield', 15)} Dynamic Attribute-Based Access Control Rules</div>
          <div class="badge badge-neutral" id="policyCountBadge">0 policies</div>
        </div>
        <div id="policyTableContainer"></div>
      </div>
    </div>
  `;

  container.querySelector('#policyRefreshBtn').addEventListener('click', loadPolicies);
  container.querySelector('#policyAddBtn').addEventListener('click', () => openPolicyModal());
  container.querySelector('#policySearch').addEventListener('input', (e) => {
    const term = (e.target.value || '').toLowerCase().trim();
    renderPolicyTable(policies.filter(p => 
      ((p.roleName || '')).toLowerCase().includes(term) || 
      ((p.allowedDepartment || '')).toLowerCase().includes(term) ||
      (Array.isArray(p.allowedIpRanges) && p.allowedIpRanges.some(ip => (ip || '').toLowerCase().includes(term)))
    ));
  });

  loadPolicies();
}

async function loadPolicies() {
  const container = document.getElementById('policyTableContainer');
  if (!container) return;
  setLoading(container, true);

  try {
    const [pList, rList] = await Promise.all([
      api.getPolicies(),
      api.getPolicyRoles().catch(() => [])
    ]);

    policies = Array.isArray(pList) ? pList : [];
    roles = Array.isArray(rList) ? rList : [];

    renderPolicyStats();
    renderPolicyTable(policies);
    const badge = document.getElementById('policyCountBadge');
    if (badge) badge.textContent = `${policies.length} policies`;
  } catch (err) {
    container.innerHTML = `<div class="empty-state">
      <div class="empty-title">Failed to load access policies</div>
      <div class="empty-sub">${escapeHtml(err.message || 'An unexpected error occurred.')}</div>
    </div>`;
  }
}

function renderPolicyStats() {
  const statsHost = document.getElementById('policyStats');
  if (!statsHost) return;

  const total = policies.length;
  const active = policies.filter(p => p.isActive).length;
  const shiftWindowed = policies.filter(p => p.startTime && p.endTime).length;
  const unrestricted = policies.filter(p => !Array.isArray(p.allowedIpRanges) || p.allowedIpRanges.includes('*') || p.allowedIpRanges.length === 0).length;

  statsHost.innerHTML = `
    <div class="stat-card">
      <div class="stat-label">Total ABAC Policies</div>
      <div class="stat-val">${total}</div>
      <div class="stat-sub">Across system roles</div>
    </div>
    <div class="stat-card">
      <div class="stat-label">Active Enforcement</div>
      <div class="stat-val">${active}</div>
      <div class="stat-sub">Enforced on RAG queries</div>
    </div>
    <div class="stat-card">
      <div class="stat-label">Shift-Window Constrained</div>
      <div class="stat-val">${shiftWindowed}</div>
      <div class="stat-sub">Time-gated operational hours</div>
    </div>
    <div class="stat-card">
      <div class="stat-label">Unrestricted / Global</div>
      <div class="stat-val">${unrestricted}</div>
      <div class="stat-sub">Wide IP range clearance</div>
    </div>
  `;
}

function renderPolicyTable(list) {
  const container = document.getElementById('policyTableContainer');
  if (!container) return;

  if (!list || list.length === 0) {
    container.innerHTML = `<div class="empty-state">
      <div class="empty-title">No policies found</div>
      <div class="empty-sub">Create your first dynamic ABAC rule to govern shift hours and network access.</div>
    </div>`;
    return;
  }

  const rows = list.map(p => {
    const shift = p.startTime && p.endTime
      ? `<span class="badge badge-info">${escapeHtml(p.startTime)} - ${escapeHtml(p.endTime)} UTC</span>`
      : `<span class="badge badge-neutral">24/7 (Unrestricted)</span>`;

    const ips = (Array.isArray(p.allowedIpRanges) && p.allowedIpRanges.length > 0)
      ? p.allowedIpRanges.map(ip => `<span class="badge ${ip === '*' ? 'badge-warning' : 'badge-neutral'}">${escapeHtml(ip)}</span>`).join(' ')
      : `<span class="badge badge-neutral">Any IP</span>`;

    const countries = (Array.isArray(p.allowedCountries) && p.allowedCountries.length > 0)
      ? p.allowedCountries.join(', ')
      : 'Any';

    const statusPill = p.isActive
      ? `<span class="badge badge-success">Active</span>`
      : `<span class="badge badge-neutral">Inactive</span>`;

    return `
      <tr>
        <td style="font-weight:600">${escapeHtml(p.roleName)}</td>
        <td><span class="badge badge-primary">${escapeHtml(p.allowedDepartment || 'All')}</span></td>
        <td>${shift}</td>
        <td>${ips}</td>
        <td style="color:var(--text-muted);font-size:12px">${escapeHtml(countries)}</td>
        <td>${statusPill}</td>
        <td style="text-align:right">
          <button class="btn btn-ghost btn-xs toggle-policy-btn" data-id="${p.policyId}" title="Toggle active status">
            ${p.isActive ? 'Deactivate' : 'Activate'}
          </button>
          <button class="btn btn-ghost btn-xs edit-policy-btn" data-id="${p.policyId}">Edit</button>
          <button class="btn btn-ghost btn-xs danger delete-policy-btn" data-id="${p.policyId}">Delete</button>
        </td>
      </tr>
    `;
  }).join('');

  container.innerHTML = `
    <div class="table-responsive">
      <table class="table">
        <thead>
          <tr>
            <th>Role</th>
            <th>Department</th>
            <th>Authorized Shift (UTC)</th>
            <th>Allowed IP Subnets</th>
            <th>Countries</th>
            <th>Status</th>
            <th style="text-align:right">Actions</th>
          </tr>
        </thead>
        <tbody>
          ${rows}
        </tbody>
      </table>
    </div>
  `;

  container.querySelectorAll('.toggle-policy-btn').forEach(btn => {
    btn.addEventListener('click', async () => {
      try {
        await api.togglePolicy(btn.dataset.id);
        toast('Policy status updated', 'success');
        loadPolicies();
      } catch (err) {
        toast(err.message || 'Failed to toggle policy', 'error');
      }
    });
  });

  container.querySelectorAll('.edit-policy-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const p = policies.find(x => x.policyId === btn.dataset.id);
      if (p) openPolicyModal(p);
    });
  });

  container.querySelectorAll('.delete-policy-btn').forEach(btn => {
    btn.addEventListener('click', async () => {
      if (!confirm('Are you sure you want to delete this access policy?')) return;
      try {
        await api.deletePolicy(btn.dataset.id);
        toast('Policy deleted', 'success');
        loadPolicies();
      } catch (err) {
        toast(err.message || 'Failed to delete policy', 'error');
      }
    });
  });
}

function openPolicyModal(policy = null) {
  const isEdit = !!policy;
  const host = document.getElementById('modalHost');

  const roleOptions = roles.map(r => 
    `<option value="${r.roleId}" ${policy && policy.roleId === r.roleId ? 'selected' : ''}>${escapeHtml(r.roleName)} (Clearance L${r.clinicalClearance})</option>`
  ).join('');

  const ipString = policy && policy.allowedIpRanges ? policy.allowedIpRanges.join(', ') : '127.0.0.1, ::1, *';
  const countryString = policy && policy.allowedCountries ? policy.allowedCountries.join(', ') : 'EG, US, *';

  host.innerHTML = `
    <div class="modal-backdrop open" id="policyModalBackdrop">
      <div class="modal" style="max-width:560px">
        <div class="modal-header">
          <div class="modal-title">${icon('shield', 16)} ${isEdit ? 'Edit Access Policy' : 'Create Access Policy'}</div>
          <button class="icon-btn" id="closePolicyModalBtn">${icon('x', 15)}</button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label>Target Role</label>
            <select class="input" id="polRoleId">
              ${roleOptions}
            </select>
          </div>
          <div class="form-group">
            <label>Authorized Department (or '*' for any)</label>
            <input class="input" id="polDept" value="${escapeHtml(policy?.allowedDepartment || '*')}" placeholder="e.g. CyberSecurity or *">
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Shift Start Time (UTC, HH:mm)</label>
              <input class="input" type="time" id="polStartTime" value="${policy?.startTime || ''}">
            </div>
            <div class="form-group">
              <label>Shift End Time (UTC, HH:mm)</label>
              <input class="input" type="time" id="polEndTime" value="${policy?.endTime || ''}">
            </div>
          </div>
          <div class="form-group">
            <label>Allowed IP Ranges (comma-separated CIDR, IPs, or '*')</label>
            <input class="input" id="polIps" value="${escapeHtml(ipString)}" placeholder="127.0.0.1, ::1, 192.168.1.0/24, *">
          </div>
          <div class="form-group">
            <label>Allowed Countries (comma-separated ISO codes, or '*')</label>
            <input class="input" id="polCountries" value="${escapeHtml(countryString)}" placeholder="EG, US, *">
          </div>
          <div class="form-group" style="display:flex;align-items:center;gap:8px;margin-top:8px">
            <input type="checkbox" id="polActive" ${(policy ? policy.isActive : true) ? 'checked' : ''}>
            <label for="polActive" style="margin:0;cursor:pointer">Enforce this policy actively</label>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-ghost" id="cancelPolicyBtn">Cancel</button>
          <button class="btn btn-primary" id="savePolicyBtn">${isEdit ? 'Save Changes' : 'Create Policy'}</button>
        </div>
      </div>
    </div>
  `;

  const close = () => { host.innerHTML = ''; };
  host.querySelector('#closePolicyModalBtn').addEventListener('click', close);
  host.querySelector('#cancelPolicyBtn').addEventListener('click', close);

  host.querySelector('#savePolicyBtn').addEventListener('click', async () => {
    const roleId = host.querySelector('#polRoleId').value;
    const dept = host.querySelector('#polDept').value.trim();
    const startTime = host.querySelector('#polStartTime').value;
    const endTime = host.querySelector('#polEndTime').value;
    const ips = host.querySelector('#polIps').value.split(',').map(s => s.trim()).filter(Boolean);
    const countries = host.querySelector('#polCountries').value.split(',').map(s => s.trim()).filter(Boolean);
    const isActive = host.querySelector('#polActive').checked;

    const payload = {
      roleId,
      allowedDepartment: dept,
      startTime: startTime || null,
      endTime: endTime || null,
      allowedIpRanges: ips,
      allowedCountries: countries,
      isActive
    };

    try {
      if (isEdit) {
        await api.updatePolicy(policy.policyId, payload);
        toast('Policy updated successfully', 'success');
      } else {
        await api.createPolicy(payload);
        toast('Policy created successfully', 'success');
      }
      close();
      loadPolicies();
    } catch (err) {
      toast(err.message || 'Failed to save policy', 'error');
    }
  });
}
