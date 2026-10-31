/* ============================================================================
 * HRMATDShifts – Searchable/Cascading Top Filters & Filtered Report Generator
 * (PDF Preview Modal, PDF Download, and Modern .xlsx Excel Download)
 * NO BACKGROUND COLOR in any report (clean black & white print/export layout)
 * ============================================================================ */
(function () {
    if (typeof window === 'undefined' || window.__hrmAtdShiftsReportLoaded) return;
    window.__hrmAtdShiftsReportLoaded = true;

    // Ensure Delete Confirmation Modal (#ovDelete) is perfectly centered horizontally & vertically
    const centerStyle = document.createElement('style');
    centerStyle.textContent = `
        #ovDelete.ov.ov-delete.open {
            display: flex !important;
            align-items: center !important;
            justify-content: center !important;
        }
        #ovDelete .modal-box.delete-modal-box {
            width: 100% !important;
            max-width: 450px !important;
            margin: 0 auto !important;
        }
    `;
    document.head.appendChild(centerStyle);

    // Ensure q and qAll are available on window for inline onclick handlers
    if (typeof window.q !== 'function') {
        window.q = function (s) { return document.querySelector(s); };
    }
    if (typeof window.qAll !== 'function') {
        window.qAll = function (s) { return Array.prototype.slice.call(document.querySelectorAll(s)); };
    }

    // Update menu button label to .xlsx if present
    window.qAll('#exp .menu button').forEach(btn => {
        if (btn.textContent && btn.textContent.includes('.xls')) {
            btn.textContent = '📊 Download Excel (.xlsx)';
        }
    });

    // Alias updateTopFilterCascade -> applyTopFilterCascade for compatibility
    window.updateTopFilterCascade = function (changedKey) {
        if (typeof applyTopFilterCascade === 'function') {
            applyTopFilterCascade(changedKey);
        }
    };

    /* ===== DATE HELPERS: dd/mm/yyyy UI DISPLAY <-> YYYY-MM-DD ISO ===== */
    function formatDateDDMMYYYY(val) {
        if (!val || val === '-') return '';
        const s = String(val).trim();
        const mIso = s.match(/^(\d{4})-(\d{1,2})-(\d{1,2})/);
        if (mIso) {
            return `${mIso[3].padStart(2, '0')}/${mIso[2].padStart(2, '0')}/${mIso[1]}`;
        }
        const mSlash = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})/);
        if (mSlash) {
            return `${mSlash[1].padStart(2, '0')}/${mSlash[2].padStart(2, '0')}/${mSlash[3]}`;
        }
        return s;
    }

    function toIsoDateStr(val) {
        if (!val) return new Date().toISOString().slice(0, 10);
        const s = String(val).trim();
        const mIso = s.match(/^(\d{4})-(\d{1,2})-(\d{1,2})/);
        if (mIso) {
            return `${mIso[1]}-${mIso[2].padStart(2, '0')}-${mIso[3].padStart(2, '0')}`;
        }
        const mSlash = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})/);
        if (mSlash) {
            return `${mSlash[3]}-${mSlash[2].padStart(2, '0')}-${mSlash[1].padStart(2, '0')}`;
        }
        return new Date().toISOString().slice(0, 10);
    }

    function setupEffDateInputDDMMYYYY() {
        const effInput = window.q('#eff');
        if (!effInput || effInput.dataset.ddmmyyyyReady === '1') return;
        effInput.dataset.ddmmyyyyReady = '1';

        const initIso = toIsoDateStr(effInput.value);
        effInput.type = 'text';
        effInput.placeholder = 'dd/mm/yyyy';
        effInput.maxLength = 10;
        effInput.autocomplete = 'off';
        effInput.value = formatDateDDMMYYYY(initIso);

        const parent = effInput.parentNode;
        const wrap = document.createElement('div');
        wrap.style.cssText = 'position:relative;display:flex;align-items:center;width:100%;';
        parent.insertBefore(wrap, effInput);
        wrap.appendChild(effInput);
        effInput.style.paddingRight = '34px';

        const nativePicker = document.createElement('input');
        nativePicker.type = 'date';
        nativePicker.id = 'effNativePicker';
        nativePicker.tabIndex = -1;
        nativePicker.value = initIso;
        nativePicker.style.cssText = 'position:absolute;right:6px;width:26px;height:26px;opacity:0;pointer-events:none;border:0;padding:0;';
        wrap.appendChild(nativePicker);

        const calBtn = document.createElement('button');
        calBtn.type = 'button';
        calBtn.id = 'effCalBtn';
        calBtn.title = 'Pick date';
        calBtn.innerHTML = '📅';
        calBtn.style.cssText = 'position:absolute;right:6px;top:50%;transform:translateY(-50%);border:none;background:transparent;cursor:pointer;font-size:14px;line-height:1;padding:2px 4px;color:#475569;';
        calBtn.addEventListener('click', function () {
            if (effInput.disabled) return;
            nativePicker.value = toIsoDateStr(effInput.value);
            if (typeof nativePicker.showPicker === 'function') {
                try { nativePicker.showPicker(); } catch (e) { nativePicker.focus(); nativePicker.click(); }
            } else {
                nativePicker.focus();
                nativePicker.click();
            }
        });
        wrap.appendChild(calBtn);

        nativePicker.addEventListener('change', function () {
            if (nativePicker.value) {
                effInput.value = formatDateDDMMYYYY(nativePicker.value);
                effInput.classList.remove('err');
            }
        });

        effInput.addEventListener('blur', function () {
            const v = effInput.value.trim();
            if (v) {
                const formatted = formatDateDDMMYYYY(v);
                effInput.value = formatted;
                nativePicker.value = toIsoDateStr(formatted);
            }
        });
    }

    setupEffDateInputDDMMYYYY();

    function detailMatchesTopFilter(d) {
        if (typeof F_MS === 'undefined') return true;
        const selCo = (F_MS['fCo'] || []).map(x => String(x).trim()).filter(Boolean);
        const selBr = (F_MS['fBr'] || []).map(x => String(x).trim()).filter(Boolean);
        const selDp = (F_MS['fDp'] || []).map(x => String(x).trim()).filter(Boolean);
        const selEmp = (F_MS['fEmp'] || []).map(x => String(x).trim()).filter(Boolean);

        if (selCo.length > 0) {
            const dCo = (d.company || []).map(x => String(x).trim()).filter(Boolean);
            if (dCo.length === 0 || !dCo.some(c => selCo.includes(c))) return false;
        }
        if (selBr.length > 0) {
            const dBr = (d.branch || []).map(x => String(x).trim()).filter(Boolean);
            if (dBr.length === 0 || !dBr.some(b => selBr.includes(b))) return false;
        }
        if (selDp.length > 0) {
            const dDp = (d.dept || []).map(x => String(x).trim()).filter(Boolean);
            if (dDp.length === 0 || !dDp.some(dp => selDp.includes(dp))) return false;
        }
        if (selEmp.length > 0) {
            const dEmp = (d.emp || []).map(x => String(x).trim()).filter(Boolean);
            if (dEmp.length === 0 || !dEmp.some(em => selEmp.includes(em))) return false;
        }
        return true;
    }

    function hasActiveDetailTopFilter() {
        if (typeof F_MS === 'undefined') return false;
        return (
            (F_MS['fCo'] && F_MS['fCo'].length > 0) ||
            (F_MS['fBr'] && F_MS['fBr'].length > 0) ||
            (F_MS['fDp'] && F_MS['fDp'].length > 0) ||
            (F_MS['fEmp'] && F_MS['fEmp'].length > 0)
        );
    }

    function getFilteredDetails(s) {
        const allDetails = (Array.isArray(s.details) ? s.details : []).map((d, idx) => ({ ...d, _origIdx: idx }));
        if (!hasActiveDetailTopFilter()) return allDetails;
        return allDetails.filter(d => detailMatchesTopFilter(d));
    }

    function filteredOverride() {
        const qSearch = ((window.q('#ls')?.value || '') + ' ' + (window.q('#gs')?.value || '')).toLowerCase().trim();
        const fTypeVal = (window.q('#fType')?.value || '').trim();
        const fStVal = (window.q('#fSt')?.value || '').trim();
        const detailFilterOn = hasActiveDetailTopFilter();
        const allShifts = typeof shifts !== 'undefined' && Array.isArray(shifts) ? shifts : [];

        const list = allShifts.filter(s => {
            if (fTypeVal && String(s.typeId).trim() !== fTypeVal && String(s.type).trim() !== fTypeVal) return false;
            if (fStVal && String(s.status).trim() !== fStVal) return false;

            if (detailFilterOn) {
                const matchingDetails = getFilteredDetails(s);
                if (matchingDetails.length === 0) return false;
            }

            if (qSearch) {
                const detailNames = (s.details || []).map(d => [
                    ...(typeof getNamesList === 'function' ? getNamesList('company', d.company) : []),
                    ...(typeof getNamesList === 'function' ? getNamesList('branch', d.branch) : []),
                    ...(typeof getNamesList === 'function' ? getNamesList('dept', d.dept) : []),
                    ...(typeof getNamesList === 'function' ? getNamesList('emp', d.emp) : [])
                ].join(' ')).join(' ');
                const rowStr = (JSON.stringify(s) + ' ' + detailNames).toLowerCase();
                return qSearch.split(/\s+/).every(w => rowStr.includes(w));
            }
            return true;
        });

        if (typeof getSortVal === 'function' && typeof sortCol !== 'undefined' && typeof sortDir !== 'undefined') {
            list.sort((a, b) => {
                let va = getSortVal(a, sortCol);
                let vb = getSortVal(b, sortCol);
                if (typeof va === 'string' && typeof vb === 'string') {
                    const cmp = va.localeCompare(vb, undefined, { numeric: true, sensitivity: 'base' });
                    return sortDir === 'asc' ? cmp : -cmp;
                }
                if (va < vb) return sortDir === 'asc' ? -1 : 1;
                if (va > vb) return sortDir === 'asc' ? 1 : -1;
                return 0;
            });
        }

        return list;
    }

    function renderOverride() {
        const L = filteredOverride();
        const pp = +(window.q('#pp')?.value || 10);
        const pages = Math.max(1, Math.ceil(L.length / pp));
        if (typeof page !== 'undefined') {
            page = Math.min(page, pages);
        }
        const curPage = typeof page !== 'undefined' ? page : 1;
        const rows = L.slice((curPage - 1) * pp, curPage * pp);

        const tb = window.q('#tb');
        if (tb) {
            tb.innerHTML = rows.map(s => {
                const details = getFilteredDetails(s);
                const isExp = typeof expandedShifts !== 'undefined' && expandedShifts.has(String(s.id));
                const typeClass = (s.type || '').toLowerCase().includes('night') ? 'Night' : 'Day';
                const statusClass = (s.status || '') === 'Active' ? 'Active' : 'Inactive';
                const shortFn = typeof short === 'function' ? short : (t => t || '');

                const firstTimingText = details.length > 0
                    ? `${shortFn(details[0].start)} – ${shortFn(details[0].end)}${details.length > 1 ? ` (+${details.length - 1} more)` : ''}`
                    : 'No timing added';

                const masterRowHtml = `
                    <tr class="master-row ${isExp ? 'expanded' : ''}">
                        <td>
                            <button type="button" class="btn-expand ${isExp ? 'open' : ''}" onclick="toggleExpand('${s.id}')" title="Click to expand/collapse shift details">
                                <span class="exp-icon">▶</span>
                                <span>${details.length}</span>
                            </button>
                        </td>
                        <td class="id">${s.id}</td>
                        <td><div class="nm">${s.name || ''}</div></td>
                        <td><span class="pill ${typeClass}">${s.type || ''}</span></td>
                        <td>${s.desc || '<span class="sub">—</span>'}</td>
                        <td>
                            <span class="pill sched-cnt" onclick="toggleExpand('${s.id}')" title="Click to view all schedules">
                                🕒 ${details.length} Schedule${details.length === 1 ? '' : 's'}
                            </span>
                            <div class="sub" style="margin-top:2px;">${firstTimingText}</div>
                        </td>
                        <td>${s.rem || '<span class="sub">—</span>'}</td>
                        <td><span class="pill ${statusClass}">${s.status || 'Active'}</span></td>
                        <td class="act">
                            <button type="button" class="ib" onclick="openStandaloneDetailModal('add', '${s.id}')" title="Add new timing to this shift">＋ Add Time</button>
                            <button type="button" class="ib" onclick="openForm('${s.id}')">✎ Edit</button>
                            <button type="button" class="ib d" onclick="del('${s.id}')">🗑 Delete</button>
                        </td>
                    </tr>
                `;

                if (!isExp) return masterRowHtml;

                const detailRowsHtml = details.length > 0
                    ? details.map((d, dispIdx) => {
                        const origIdx = d._origIdx !== undefined ? d._origIdx : dispIdx;
                        const dStatusClass = (d.status || 'Active') === 'Active' ? 'Active' : 'Inactive';
                        const otText = (!d.otRule || d.otRule === 'Not Applicable' || !d.otRuleId)
                            ? '<span class="sub">Not Applicable</span>'
                            : `<b>${d.otRule}</b><div class="sub">After ${d.otAfter || 60}m | Max ${d.otMax || 4}h | Min ${d.otMin || 50}m</div>`;
                        const badgesHtml = typeof renderApplBadges === 'function' ? renderApplBadges(d, 'main', s.id, origIdx) : '';
                        return `
                            <tr>
                                <td><b>#${dispIdx + 1}</b></td>
                                <td>
                                    <b>${shortFn(d.start)} – ${shortFn(d.end)}</b>
                                    ${d.cross ? '<span class="pill cm" title="Cross midnight">+1d</span>' : ''}
                                </td>
                                <td>${shortFn(d.late)}</td>
                                <td>${shortFn(d.absent)}</td>
                                <td>${shortFn(d.bf)} – ${shortFn(d.bt)}</td>
                                <td>${shortFn(d.early)}</td>
                                <td class="appl-td">${badgesHtml}</td>
                                <td>${otText}</td>
                                <td>${formatDateDDMMYYYY(d.eff)}</td>
                                <td>${d.rem || '<span class="sub">—</span>'}</td>
                                <td><span class="pill ${dStatusClass}">${d.status || 'Active'}</span></td>
                                <td class="act">
                                    <button type="button" class="ib v" onclick="openStandaloneDetailModal('view', '${s.id}', ${origIdx})">👁 View</button>
                                    <button type="button" class="ib" onclick="openStandaloneDetailModal('edit', '${s.id}', ${origIdx})">✎ Edit</button>
                                    <button type="button" class="ib d" onclick="deleteStandaloneDetail('${s.id}', ${origIdx})">🗑 Delete</button>
                                </td>
                            </tr>
                        `;
                    }).join('')
                    : `<tr><td colspan="12" class="empty">No timing details added for this shift yet. Click "＋ Add Time" to add one.</td></tr>`;

                const expandRowHtml = `
                    <tr class="detail-expand-row">
                        <td colspan="9">
                            <div class="sub-card">
                                <div class="sub-card-head">
                                    <span>📋 Shift Details &amp; Timings for: ${s.name} (Shift ID: ${s.id})</span>
                                    <button type="button" class="ib" onclick="openStandaloneDetailModal('add', '${s.id}')">＋ Add Shift Details</button>
                                </div>
                                <div class="shift-table-wrap">
                                    <table class="detail-sub-table">
                                        <thead>
                                            <tr>
                                                <th>#</th>
                                                <th>Start – End</th>
                                                <th>Late</th>
                                                <th>Absent</th>
                                                <th>Break</th>
                                                <th>Early Leave Before</th>
                                                <th>Applicable To</th>
                                                <th>Overtime Rules</th>
                                                <th>Effective From</th>
                                                <th>Remarks</th>
                                                <th>Activity Status</th>
                                                <th>Actions</th>
                                            </tr>
                                        </thead>
                                        <tbody>${detailRowsHtml}</tbody>
                                    </table>
                                </div>
                            </div>
                        </td>
                    </tr>
                `;

                return masterRowHtml + expandRowHtml;
            }).join('') || '<tr><td colspan="9" class="empty">No shifts found</td></tr>';
        }

        const info = window.q('#info');
        if (info) {
            info.textContent = `Showing ${L.length ? (curPage - 1) * pp + 1 : 0} to ${Math.min(curPage * pp, L.length)} of ${L.length} entries`;
        }

        const pg = window.q('#pg');
        if (pg) {
            let h = `<button type="button" ${curPage == 1 ? 'disabled' : ''} onclick="gp(${curPage - 1})">Prev</button>`;
            for (let p = 1; p <= pages; p++) {
                if (p == 1 || p == pages || Math.abs(p - curPage) <= 1)
                    h += `<button type="button" class="${p == curPage ? 'on' : ''}" onclick="gp(${p})">${p}</button>`;
                else if (Math.abs(p - curPage) == 2)
                    h += '<button type="button" disabled>…</button>';
            }
            pg.innerHTML = h + `<button type="button" ${curPage == pages ? 'disabled' : ''} onclick="gp(${curPage + 1})">Next</button>`;
        }
        if (typeof kpis === 'function') kpis();
        if (typeof renderSortHeaders === 'function') renderSortHeaders();
    }

    function kfOverride(x) {
        resetFOverride(1);
        if (x) {
            const [a, b] = x.split('=');
            if (a === 'fType' && typeof setSingleFilterValue === 'function') {
                setSingleFilterValue('#msFType', '#fType', 'Shift type: All', b);
            } else if (a === 'fSt' && typeof setSingleFilterValue === 'function') {
                setSingleFilterValue('#msFSt', '#fSt', 'Activity status: All', b);
            }
        }
        if (typeof page !== 'undefined') page = 1;
        renderOverride();
    }

    function resetFOverride(qFlag) {
        if (typeof setSingleFilterValue === 'function') {
            setSingleFilterValue('#msFType', '#fType', 'Shift type: All', '');
            setSingleFilterValue('#msFSt', '#fSt', 'Activity status: All', '');
        }
        if (typeof F_MS !== 'undefined') {
            F_MS.fCo = [];
            F_MS.fBr = [];
            F_MS.fDp = [];
            F_MS.fEmp = [];
        }
        const fCoEl = window.q('#msFCo');
        if (fCoEl && typeof rebuildMultiFilterOptions === 'function' && typeof renderMultiFilterMS === 'function') {
            rebuildMultiFilterOptions(fCoEl, 'fCo', 'Company');
            renderMultiFilterMS(fCoEl, 'fCo', 'Company');
        }
        if (typeof applyTopFilterCascade === 'function') {
            applyTopFilterCascade('fCo');
        }
        if (!qFlag) {
            if (window.q('#ls')) window.q('#ls').value = '';
            if (window.q('#gs')) window.q('#gs').value = '';
            if (typeof page !== 'undefined') page = 1;
            renderOverride();
        }
    }

    function formatNowddMMyyyy() {
        const d = new Date();
        const pad = n => String(n).padStart(2, '0');
        return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }

    function getActiveFilterReportMeta() {
        const fTypeVal = (window.q('#fType')?.value || '').trim();
        const fStVal = (window.q('#fSt')?.value || '').trim();
        const qSearch = ((window.q('#ls')?.value || '') + ' ' + (window.q('#gs')?.value || '')).trim();

        const stList = typeof shiftTypeOpts !== 'undefined' ? shiftTypeOpts : [];
        const typeText = fTypeVal
            ? (stList.find(t => String(t.id).trim() === fTypeVal)?.name || fTypeVal)
            : 'All';
        const statusText = fStVal || 'All';

        const getNames = typeof getNamesList === 'function' ? getNamesList : (() => []);
        const coNames = typeof F_MS !== 'undefined' ? getNames('company', F_MS['fCo']) : [];
        const brNames = typeof F_MS !== 'undefined' ? getNames('branch', F_MS['fBr']) : [];
        const dpNames = typeof F_MS !== 'undefined' ? getNames('dept', F_MS['fDp']) : [];
        const empNames = typeof F_MS !== 'undefined' ? getNames('emp', F_MS['fEmp']) : [];

        const defaultCompName = (typeof ALL_OPT !== 'undefined' && ALL_OPT.company && ALL_OPT.company.length > 0)
            ? ALL_OPT.company[0].name
            : 'Data Path';

        const companyHeader = coNames.length === 1 ? coNames[0] : defaultCompName;

        return {
            companyHeader,
            shiftType: typeText,
            company: coNames.length ? coNames.join(', ') : 'All',
            branch: brNames.length ? brNames.join(', ') : 'All',
            department: dpNames.length ? dpNames.join(', ') : 'All',
            employee: empNames.length
                ? (empNames.length > 5 ? `${empNames.slice(0, 5).join(', ')} (+${empNames.length - 5} more)` : empNames.join(', '))
                : 'All',
            status: statusText,
            search: qSearch || 'None',
            generatedAt: formatNowddMMyyyy()
        };
    }

    function getFilteredReportRows() {
        const L = filteredOverride();
        const rows = [];
        let sl = 1;
        const getNames = typeof getNamesList === 'function' ? getNamesList : (() => []);
        const shortFn = typeof short === 'function' ? short : (t => t || '');

        L.forEach(s => {
            const details = getFilteredDetails(s);
            const listToUse = details.length > 0 ? details : [{}];
            listToUse.forEach((d, idx) => {
                const compNames = getNames('company', d.company);
                const brNames = getNames('branch', d.branch);
                const deptNames = getNames('dept', d.dept);
                const empNames = getNames('emp', d.emp);

                const compStr = compNames.length ? compNames.join(', ') : 'All Company';
                const brStr = brNames.length ? brNames.join(', ') : 'All Branch';
                const deptStr = deptNames.length ? deptNames.join(', ') : 'All Department';
                const empStr = empNames.length
                    ? (empNames.length > 4 ? `${empNames.slice(0, 4).join(', ')} (+${empNames.length - 4} more)` : empNames.join(', '))
                    : 'All Employee';

                const hasOt = !!(d.otRule && d.otRule !== 'Not Applicable' && d.otRuleId);
                const otRuleName = hasOt ? d.otRule : 'Not Applicable';
                const otSummaryText = hasOt
                    ? `${d.otRule} (After ${d.otAfter ?? 60}m, Max ${d.otMax ?? 4}h, Min ${d.otMin ?? 50}m)`
                    : 'Not Applicable';

                rows.push({
                    sl: sl++,
                    shiftId: String(s.id || ''),
                    shiftName: String(s.name || ''),
                    shiftType: String(s.type || ''),
                    shiftDesc: String(s.desc || '-'),
                    masterStatus: String(s.status || 'Active'),
                    masterRemarks: String(s.rem || '-'),
                    scheduleNo: details.length > 0 ? `#${idx + 1}` : '-',
                    timing: d.start ? `${shortFn(d.start)} - ${shortFn(d.end)}${d.cross ? ' (+1d)' : ''}` : '-',
                    start: d.start ? shortFn(d.start) : '-',
                    end: d.end ? shortFn(d.end) : '-',
                    crossMidnight: d.cross ? 'Yes' : 'No',
                    late: d.late ? shortFn(d.late) : '-',
                    absent: d.absent ? shortFn(d.absent) : '-',
                    breakFrom: d.bf ? shortFn(d.bf) : '-',
                    breakTo: d.bt ? shortFn(d.bt) : '-',
                    breakTime: d.bf ? `${shortFn(d.bf)} - ${shortFn(d.bt)}` : '-',
                    earlyLeave: d.early ? shortFn(d.early) : '-',
                    company: compStr,
                    branch: brStr,
                    department: deptStr,
                    employee: empStr,
                    otRuleName: otRuleName,
                    otRuleSummary: otSummaryText,
                    otAfter: hasOt ? String(d.otAfter ?? 60) : '-',
                    otMax: hasOt ? String(d.otMax ?? 4) : '-',
                    otMin: hasOt ? String(d.otMin ?? 50) : '-',
                    woOT: String(d.woOT || 'Not Applicable'),
                    hOT: String(d.hOT || 'Not Applicable'),
                    effectiveFrom: formatDateDDMMYYYY(d.eff) || '-',
                    detailStatus: String(d.status || 'Active'),
                    detailRemarks: String(d.rem || '-')
                });
            });
        });

        return { masterCount: L.length, rows };
    }

    /* ===== PDF REPORT (NO BACKGROUND COLOR, PURE BLACK & WHITE) ===== */
    function buildShiftReportPdfDoc() {
        const meta = getActiveFilterReportMeta();
        const { masterCount, rows } = getFilteredReportRows();

        const tableHeader = [
            { text: 'SL', style: 'th', alignment: 'center' },
            { text: 'Shift ID', style: 'th', alignment: 'center' },
            { text: 'Shift Name &\nDescription', style: 'th', alignment: 'center' },
            { text: 'Shift\nType', style: 'th', alignment: 'center' },
            { text: 'Master\nStatus', style: 'th', alignment: 'center' },
            { text: 'Shift Timing\n(Start - End)', style: 'th', alignment: 'center' },
            { text: 'Late /\nAbsent', style: 'th', alignment: 'center' },
            { text: 'Break /\nEarly Leave', style: 'th', alignment: 'center' },
            { text: 'Company &\nBranch', style: 'th', alignment: 'center' },
            { text: 'Department &\nEmployee', style: 'th', alignment: 'center' },
            { text: 'Overtime\nRules', style: 'th', alignment: 'center' },
            { text: 'Effective\nFrom', style: 'th', alignment: 'center' },
            { text: 'Detail\nStatus', style: 'th', alignment: 'center' },
            { text: 'Remarks', style: 'th', alignment: 'center' }
        ];

        const tableBody = [tableHeader];

        rows.forEach(r => {
            tableBody.push([
                { text: String(r.sl), style: 'td', alignment: 'center' },
                { text: r.shiftId, style: 'td', alignment: 'center', bold: true },
                {
                    stack: [
                        { text: r.shiftName, bold: true },
                        { text: r.shiftDesc !== '-' ? r.shiftDesc : '', fontSize: 6.8 }
                    ],
                    style: 'td'
                },
                { text: r.shiftType, style: 'td', alignment: 'center' },
                { text: r.masterStatus, style: 'td', alignment: 'center' },
                { text: `${r.scheduleNo}: ${r.timing}`, style: 'td', alignment: 'center', bold: true },
                { text: `Late: ${r.late}\nAbs: ${r.absent}`, style: 'td', alignment: 'center' },
                { text: `Brk: ${r.breakTime}\nEL: ${r.earlyLeave}`, style: 'td', alignment: 'center' },
                { text: `Co: ${r.company}\nBr: ${r.branch}`, style: 'td' },
                { text: `Dept: ${r.department}\nEmp: ${r.employee}`, style: 'td' },
                { text: r.otRuleSummary, style: 'td' },
                { text: r.effectiveFrom, style: 'td', alignment: 'center' },
                { text: r.detailStatus, style: 'td', alignment: 'center' },
                { text: r.detailRemarks !== '-' ? r.detailRemarks : r.masterRemarks, style: 'td' }
            ]);
        });

        return {
            pageSize: 'A4',
            pageOrientation: 'landscape',
            pageMargins: [20, 20, 20, 28],
            content: [
                { text: meta.companyHeader, fontSize: 18, bold: true, alignment: 'center', color: '#000000' },
                { text: 'Shift Setup Report (Master & Timing Details)', fontSize: 12, bold: true, decoration: 'underline', alignment: 'center', color: '#000000', margin: [0, 2, 0, 6] },
                {
                    text: `Shift Type: ${meta.shiftType}   |   Company: ${meta.company}   |   Branch: ${meta.branch}   |   Department: ${meta.department}   |   Employee: ${meta.employee}   |   Status: ${meta.status}`,
                    fontSize: 8.5,
                    alignment: 'center',
                    color: '#000000',
                    margin: [0, 0, 0, 8]
                },
                {
                    table: {
                        headerRows: 1,
                        widths: [16, 28, 66, 38, 32, 64, 52, 62, 72, 82, 74, 44, 32, '*'],
                        body: tableBody
                    },
                    layout: {
                        hLineWidth: function () { return 0.5; },
                        vLineWidth: function () { return 0.5; },
                        hLineColor: function () { return '#000000'; },
                        vLineColor: function () { return '#000000'; },
                        paddingLeft: function () { return 3; },
                        paddingRight: function () { return 3; },
                        paddingTop: function () { return 4; },
                        paddingBottom: function () { return 4; }
                    }
                },
                {
                    text: `Total Shifts (Master): ${masterCount}     |     Total Timing Schedules (Details): ${rows.length}`,
                    fontSize: 9,
                    bold: true,
                    color: '#000000',
                    margin: [0, 8, 0, 0]
                }
            ],
            footer: function (currentPage, pageCount) {
                return {
                    columns: [
                        { text: `Generated on: ${meta.generatedAt}`, fontSize: 8, color: '#000000', margin: [20, 6, 0, 0] },
                        { text: `Page ${currentPage} of ${pageCount}`, fontSize: 8, color: '#000000', alignment: 'right', margin: [0, 6, 20, 0] }
                    ]
                };
            },
            styles: {
                th: {
                    fontSize: 7.5,
                    bold: true,
                    color: '#000000'
                },
                td: {
                    fontSize: 7,
                    color: '#000000'
                }
            },
            defaultStyle: {
                fontSize: 7.5,
                color: '#000000'
            }
        };
    }

    let currentPdfBlobUrl = null;

    function showMsg(msg) {
        if (typeof toast === 'function') {
            toast(msg);
        } else if (typeof toastr !== 'undefined') {
            toastr.info(msg);
        } else {
            alert(msg);
        }
    }

    function previewPdfReport() {
        try {
            window.q('#exp')?.classList.remove('open');
            const { rows } = getFilteredReportRows();
            if (!rows.length) {
                return showMsg('No shift records match the current filter');
            }
            if (typeof pdfMake === 'undefined') {
                return showMsg('PDF library is loading, please try again.');
            }

            const docDef = buildShiftReportPdfDoc();
            window.q('#ovPdfPreview')?.classList.add('open');
            document.body.style.overflow = 'hidden';

            pdfMake.createPdf(docDef).getBlob(function (blob) {
                if (currentPdfBlobUrl) {
                    URL.revokeObjectURL(currentPdfBlobUrl);
                }
                currentPdfBlobUrl = URL.createObjectURL(blob);
                const frame = window.q('#pdfPreviewFrame');
                if (frame) frame.src = currentPdfBlobUrl;
            });
        } catch (err) {
            console.error('PDF Preview Error:', err);
            showMsg('Failed to generate PDF preview: ' + (err.message || err));
        }
    }

    function closePdfPreviewModal() {
        try {
            window.q('#ovPdfPreview')?.classList.remove('open');
            document.body.style.overflow = '';
            const frame = window.q('#pdfPreviewFrame');
            if (frame) frame.src = 'about:blank';
        } catch (err) {
            console.error(err);
        }
    }

    function downloadPdfReport() {
        try {
            window.q('#exp')?.classList.remove('open');
            const { rows } = getFilteredReportRows();
            if (!rows.length) {
                return showMsg('No shift records match the current filter');
            }
            if (typeof pdfMake === 'undefined') {
                return showMsg('PDF library is loading, please try again.');
            }

            const docDef = buildShiftReportPdfDoc();
            const dateStamp = new Date().toISOString().slice(0, 10);
            pdfMake.createPdf(docDef).download(`Shift_Setup_Report_${dateStamp}.pdf`);
            showMsg('PDF report downloaded');
        } catch (err) {
            console.error('PDF Download Error:', err);
            showMsg('Failed to download PDF: ' + (err.message || err));
        }
    }

    /* ===== MODERN .XLSX EXCEL GENERATOR (NO BACKGROUND COLOR, THIN BLACK BORDERS) ===== */
    function xmlEsc(v) {
        return String(v ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&apos;');
    }

    function colLetter(colIdx1Based) {
        let n = colIdx1Based;
        let s = '';
        while (n > 0) {
            const m = (n - 1) % 26;
            s = String.fromCharCode(65 + m) + s;
            n = Math.floor((n - 1) / 26);
        }
        return s;
    }

    function exportExcelReport() {
        try {
            window.q('#exp')?.classList.remove('open');
            const meta = getActiveFilterReportMeta();
            const { masterCount, rows } = getFilteredReportRows();
            if (!rows.length) {
                return showMsg('No shift records match the current filter');
            }

            const headers = [
                'SL',
                'Shift ID',
                'Shift Name',
                'Shift Type',
                'Description',
                'Master Status',
                'Administrative Remarks',
                'Schedule #',
                'Start Time',
                'End Time',
                'Cross Midnight',
                'Late Time',
                'Absent Time',
                'Break From',
                'Break To',
                'Early Leave Before',
                'Company',
                'Branch',
                'Department',
                'Employee',
                'Overtime Rule',
                'OT Start After (min)',
                'Max OT/Day (hrs)',
                'Min OT (min)',
                'Weekly-Off OT',
                'Holiday OT',
                'Effective From',
                'Detail Status',
                'Detail Remarks'
            ];

            const colWidths = [
                6, 10, 22, 14, 24, 14, 22, 12, 14, 14, 14, 14, 14, 14, 14, 16,
                18, 18, 22, 28, 22, 18, 16, 14, 16, 16, 15, 14, 24
            ];

            const totalCols = headers.length;
            const lastColLetter = colLetter(totalCols);
            const dateStamp = new Date().toISOString().slice(0, 10);
            const fileName = `Shift_Setup_Report_${dateStamp}.xlsx`;

            // If JSZip is available, build a styled OpenXML .xlsx with NO background color, bold headers, and thin black borders
            if (typeof JSZip !== 'undefined') {
                // Styles:
                // s="0": Default
                // s="1": Company Title (16pt Bold, Centered, No Fill, No Border)
                // s="2": Report Subtitle (12pt Bold Underline, Centered, No Fill, No Border)
                // s="3": Filter Summary (10pt Regular, Centered, No Fill, No Border)
                // s="4": Table Column Header (10pt Bold, Centered, Thin Black Border, NO Background Color)
                // s="5": Table Data Cell - Left Aligned (10pt, Thin Black Border, NO Background Color, WrapText)
                // s="6": Table Data Cell - Center Aligned (10pt, Thin Black Border, NO Background Color, WrapText)
                // s="7": Footer / Total Row (10pt Bold, Left Aligned, No Fill, No Border)
                const stylesXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
    <fonts count="5">
        <font><sz val="10"/><color rgb="FF000000"/><name val="Calibri"/></font>
        <font><b/><sz val="16"/><color rgb="FF000000"/><name val="Calibri"/></font>
        <font><b/><u/><sz val="12"/><color rgb="FF000000"/><name val="Calibri"/></font>
        <font><b/><sz val="10"/><color rgb="FF000000"/><name val="Calibri"/></font>
        <font><i/><sz val="9"/><color rgb="FF000000"/><name val="Calibri"/></font>
    </fonts>
    <fills count="2">
        <fill><patternFill patternType="none"/></fill>
        <fill><patternFill patternType="gray125"/></fill>
    </fills>
    <borders count="2">
        <border><left/><right/><top/><bottom/><diagonal/></border>
        <border>
            <left style="thin"><color rgb="FF000000"/></left>
            <right style="thin"><color rgb="FF000000"/></right>
            <top style="thin"><color rgb="FF000000"/></top>
            <bottom style="thin"><color rgb="FF000000"/></bottom>
            <diagonal/>
        </border>
    </borders>
    <cellStyleXfs count="1">
        <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
    </cellStyleXfs>
    <cellXfs count="9">
        <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
        <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1">
            <alignment horizontal="center" vertical="center"/>
        </xf>
        <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1">
            <alignment horizontal="center" vertical="center"/>
        </xf>
        <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1">
            <alignment horizontal="center" vertical="center"/>
        </xf>
        <xf numFmtId="0" fontId="3" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
            <alignment horizontal="center" vertical="center" wrapText="1"/>
        </xf>
        <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
            <alignment horizontal="left" vertical="center" wrapText="1"/>
        </xf>
        <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
            <alignment horizontal="center" vertical="center" wrapText="1"/>
        </xf>
        <xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1">
            <alignment horizontal="left" vertical="center"/>
        </xf>
        <xf numFmtId="0" fontId="4" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1">
            <alignment horizontal="left" vertical="center"/>
        </xf>
    </cellXfs>
</styleSheet>`;

                function buildRowXml(rowNum, cells, heightPt) {
                    const htAttr = heightPt ? ` ht="${heightPt}" customHeight="1"` : '';
                    const cellsXml = cells.map(c => {
                        const ref = `${colLetter(c.col)}${rowNum}`;
                        const styleAttr = c.s != null ? ` s="${c.s}"` : '';
                        if (typeof c.v === 'number' && !isNaN(c.v)) {
                            return `<c r="${ref}"${styleAttr}><v>${c.v}</v></c>`;
                        }
                        return `<c r="${ref}" t="inlineStr"${styleAttr}><is><t>${xmlEsc(c.v)}</t></is></c>`;
                    }).join('');
                    return `<row r="${rowNum}"${htAttr}>${cellsXml}</row>`;
                }

                function buildMergedBannerRow(rowNum, text, styleId, heightPt) {
                    const cells = [{ col: 1, v: text, s: styleId }];
                    for (let c = 2; c <= totalCols; c++) {
                        cells.push({ col: c, v: '', s: styleId });
                    }
                    return buildRowXml(rowNum, cells, heightPt);
                }

                const sheetRows = [];
                // Row 1: Company Name
                sheetRows.push(buildMergedBannerRow(1, meta.companyHeader, 1, 24));
                // Row 2: Report Title
                sheetRows.push(buildMergedBannerRow(2, 'Shift Setup Report (Master & Timing Details)', 2, 20));
                // Row 3: Filter Summary
                const filterLine = `Shift Type: ${meta.shiftType}  |  Company: ${meta.company}  |  Branch: ${meta.branch}  |  Department: ${meta.department}  |  Employee: ${meta.employee}  |  Status: ${meta.status}`;
                sheetRows.push(buildMergedBannerRow(3, filterLine, 3, 18));
                // Row 4: Blank spacer row
                sheetRows.push(`<row r="4" ht="8" customHeight="1"/>`);
                // Row 5: Table Headers (Bold, Centered, Thin Black Border, NO Background Color)
                sheetRows.push(buildRowXml(5, headers.map((h, i) => ({ col: i + 1, v: h, s: 4 })), 26));

                // Center-aligned column indices (1-based)
                const centerColsSet = new Set([1, 2, 4, 6, 8, 9, 10, 11, 12, 13, 14, 15, 16, 22, 23, 24, 25, 26, 27, 28]);

                let currentRow = 6;
                rows.forEach(r => {
                    const vals = [
                        r.sl,
                        r.shiftId,
                        r.shiftName,
                        r.shiftType,
                        r.shiftDesc,
                        r.masterStatus,
                        r.masterRemarks,
                        r.scheduleNo,
                        r.start,
                        r.end,
                        r.crossMidnight,
                        r.late,
                        r.absent,
                        r.breakFrom,
                        r.breakTo,
                        r.earlyLeave,
                        r.company,
                        r.branch,
                        r.department,
                        r.employee,
                        r.otRuleName,
                        r.otAfter,
                        r.otMax,
                        r.otMin,
                        r.woOT,
                        r.hOT,
                        r.effectiveFrom,
                        r.detailStatus,
                        r.detailRemarks
                    ];
                    const rowCells = vals.map((v, idx) => ({
                        col: idx + 1,
                        v: v,
                        s: centerColsSet.has(idx + 1) ? 6 : 5
                    }));
                    sheetRows.push(buildRowXml(currentRow++, rowCells, 20));
                });

                const totalRowIdx = currentRow++;
                sheetRows.push(buildMergedBannerRow(totalRowIdx, `Total Shifts (Master): ${masterCount}   |   Total Timing Schedules (Details): ${rows.length}`, 7, 20));

                const footerRowIdx = currentRow++;
                sheetRows.push(buildMergedBannerRow(footerRowIdx, `Generated on: ${meta.generatedAt}`, 8, 18));

                const colsXml = colWidths.map((w, i) => `<col min="${i + 1}" max="${i + 1}" width="${w}" customWidth="1"/>`).join('');

                const sheetXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
    <cols>${colsXml}</cols>
    <sheetData>${sheetRows.join('')}</sheetData>
    <mergeCells count="5">
        <mergeCell ref="A1:${lastColLetter}1"/>
        <mergeCell ref="A2:${lastColLetter}2"/>
        <mergeCell ref="A3:${lastColLetter}3"/>
        <mergeCell ref="A${totalRowIdx}:${lastColLetter}${totalRowIdx}"/>
        <mergeCell ref="A${footerRowIdx}:${lastColLetter}${footerRowIdx}"/>
    </mergeCells>
    <pageSetup paperSize="9" orientation="landscape"/>
</worksheet>`;

                const contentTypesXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
    <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
    <Default Extension="xml" ContentType="application/xml"/>
    <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
    <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
    <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
</Types>`;

                const relsXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
    <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>`;

                const workbookXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
    <sheets>
        <sheet name="Shift Setup Report" sheetId="1" r:id="rId1"/>
    </sheets>
</workbook>`;

                const workbookRelsXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
    <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
    <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>`;

                const zip = new JSZip();
                zip.file('[Content_Types].xml', contentTypesXml);
                zip.folder('_rels').file('.rels', relsXml);
                const xl = zip.folder('xl');
                xl.file('workbook.xml', workbookXml);
                xl.file('styles.xml', stylesXml);
                xl.folder('_rels').file('workbook.xml.rels', workbookRelsXml);
                xl.folder('worksheets').file('sheet1.xml', sheetXml);

                zip.generateAsync({
                    type: 'blob',
                    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
                }).then(function (blob) {
                    const a = document.createElement('a');
                    const url = URL.createObjectURL(blob);
                    a.href = url;
                    a.download = fileName;
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    setTimeout(() => URL.revokeObjectURL(url), 1500);
                    showMsg('Excel (.xlsx) report downloaded');
                });
                return;
            }

            // Fallback to SheetJS (XLSX) if JSZip is not available
            if (typeof XLSX !== 'undefined') {
                const aoa = [
                    [meta.companyHeader],
                    ['Shift Setup Report (Master & Timing Details)'],
                    [`Shift Type: ${meta.shiftType} | Company: ${meta.company} | Branch: ${meta.branch} | Department: ${meta.department} | Employee: ${meta.employee} | Status: ${meta.status}`],
                    [],
                    headers
                ];
                rows.forEach(r => {
                    aoa.push([
                        r.sl, r.shiftId, r.shiftName, r.shiftType, r.shiftDesc, r.masterStatus, r.masterRemarks,
                        r.scheduleNo, r.start, r.end, r.crossMidnight, r.late, r.absent, r.breakFrom, r.breakTo, r.earlyLeave,
                        r.company, r.branch, r.department, r.employee,
                        r.otRuleName, r.otAfter, r.otMax, r.otMin, r.woOT, r.hOT,
                        r.effectiveFrom, r.detailStatus, r.detailRemarks
                    ]);
                });
                aoa.push([]);
                aoa.push([`Total Shifts (Master): ${masterCount} | Total Timing Schedules (Details): ${rows.length}`]);
                aoa.push([`Generated on: ${meta.generatedAt}`]);

                const ws = XLSX.utils.aoa_to_sheet(aoa);
                ws['!cols'] = colWidths.map(w => ({ wch: w }));
                const wb = XLSX.utils.book_new();
                XLSX.utils.book_append_sheet(wb, ws, 'Shift Setup Report');
                XLSX.writeFile(wb, fileName);
                showMsg('Excel (.xlsx) report downloaded');
            }
        } catch (err) {
            console.error('Excel Download Error:', err);
            showMsg('Failed to download Excel: ' + (err.message || err));
        }
    }

    /* ===== OVERRIDES FOR MODAL DETAILS GRID & DATE PICKER (dd/mm/yyyy) ===== */
    function renderModalDetailsGridOverride() {
        const tb = window.q('#modalDetailsTb');
        if (!tb) return;
        if (window.q('#modalDetailBadgeCount')) {
            window.q('#modalDetailBadgeCount').textContent = typeof formDetails !== 'undefined' ? formDetails.length : 0;
        }

        if (typeof formDetails === 'undefined' || !formDetails.length) {
            tb.innerHTML = `<tr><td colspan="12" class="empty">No timing details added yet. Click "＋ Add Shift Details" to add timing &amp; attendance rules.</td></tr>`;
            return;
        }

        const shortFn = typeof short === 'function' ? short : (t => t || '');
        const badgesFn = typeof renderApplBadges === 'function' ? renderApplBadges : (() => '');

        tb.innerHTML = formDetails.map((d, idx) => {
            const dStatusClass = (d.status || 'Active') === 'Active' ? 'Active' : 'Inactive';
            const otRuleName = d.otRuleId
                ? (((typeof DB_DATA !== 'undefined' && DB_DATA.otRule) || []).find(r => String(r.id) === String(d.otRuleId))?.name || d.otRule || d.otRuleId)
                : 'Not Applicable';
            const otText = (!d.otRuleId || otRuleName === 'Not Applicable')
                ? '<span class="sub">N/A</span>'
                : `<b>${otRuleName}</b><div class="sub">Max ${d.otMax || 4}h/day</div>`;

            return `
                <tr>
                    <td><b>#${idx + 1}</b></td>
                    <td>
                        <b>${shortFn(d.start)} – ${shortFn(d.end)}</b>
                        ${d.cross ? '<span class="pill cm" title="Cross midnight">+1d</span>' : ''}
                    </td>
                    <td>${shortFn(d.late)}</td>
                    <td>${shortFn(d.absent)}</td>
                    <td>${shortFn(d.bf)} – ${shortFn(d.bt)}</td>
                    <td>${shortFn(d.early)}</td>
                    <td class="appl-td">${badgesFn(d, 'modal', 'modal', idx)}</td>
                    <td>${otText}</td>
                    <td>${formatDateDDMMYYYY(d.eff)}</td>
                    <td>${d.rem || '<span class="sub">—</span>'}</td>
                    <td><span class="pill ${dStatusClass}">${d.status || 'Active'}</span></td>
                    <td class="act">
                        <button type="button" class="ib v" onclick="openDetailModal('view', ${idx})">👁 View</button>
                        <button type="button" class="ib" onclick="openDetailModal('edit', ${idx})">✎ Edit</button>
                        <button type="button" class="ib d" onclick="removeModalDetail(${idx})">🗑 Delete</button>
                    </td>
                </tr>
            `;
        }).join('');
    }

    const origPopulateDetailModalFields = typeof window.populateDetailModalFields === 'function'
        ? window.populateDetailModalFields
        : null;

    function populateDetailModalFieldsOverride(d) {
        setupEffDateInputDDMMYYYY();
        if (origPopulateDetailModalFields) {
            origPopulateDetailModalFields(d);
        }
        const effInput = window.q('#eff');
        const nativePicker = window.q('#effNativePicker');
        const rawEff = (d && d.eff) ? d.eff : new Date().toISOString().slice(0, 10);
        const isoDate = toIsoDateStr(rawEff);
        if (effInput) {
            effInput.value = formatDateDDMMYYYY(isoDate);
        }
        if (nativePicker) {
            nativePicker.value = isoDate;
        }
    }

    const origSetDetailModalReadonly = typeof window.setDetailModalReadonly === 'function'
        ? window.setDetailModalReadonly
        : null;

    function setDetailModalReadonlyOverride(isReadonly) {
        if (origSetDetailModalReadonly) {
            origSetDetailModalReadonly(isReadonly);
        }
        const calBtn = window.q('#effCalBtn');
        if (calBtn) {
            calBtn.disabled = !!isReadonly;
            calBtn.style.opacity = isReadonly ? '0.45' : '1';
            calBtn.style.cursor = isReadonly ? 'not-allowed' : 'pointer';
        }
    }

    function toIsoDateTimeOverride(dateStr, time12h) {
        const baseDate = toIsoDateStr(dateStr);
        const m = (time12h || '').match(/(\d+):(\d+):(\d+)\s*(AM|PM)/i);
        if (!m) return `${baseDate}T09:00:00`;
        let h = parseInt(m[1], 10);
        const min = m[2].padStart(2, '0');
        const sec = m[3].padStart(2, '0');
        const ampm = m[4].toUpperCase();
        if (ampm === 'PM' && h < 12) h += 12;
        if (ampm === 'AM' && h === 12) h = 0;
        return `${baseDate}T${String(h).padStart(2, '0')}:${min}:${sec}`;
    }

    function mapDetailToBackendDtoOverride(d, shiftCode) {
        const effDate = toIsoDateStr(d.eff);
        return {
            AutoId: d.autoId || 0,
            ShiftCode: shiftCode,
            ShiftStartTime: toIsoDateTimeOverride(effDate, d.start),
            ShiftEndTime: toIsoDateTimeOverride(effDate, d.end),
            LateTime: toIsoDateTimeOverride(effDate, d.late),
            AbsentTime: toIsoDateTimeOverride(effDate, d.absent),
            LunchOutTime: toIsoDateTimeOverride(effDate, d.bf),
            LunchInTime: toIsoDateTimeOverride(effDate, d.bt),
            LunchBreakHour: d.breakHour || 0,
            EarlyLeaveTime: d.early || '',
            IsCrossMidnight: !!d.cross,
            CompanyIdsList: d.company || [],
            BranchIdsList: d.branch || [],
            DepartmentIdsList: d.dept || [],
            EmployeeIdsList: d.emp || [],
            OvertimeRuleID: d.otRuleId || '',
            OTGraceTime: d.otAfter ?? 60,
            MaxOTPerDay: d.otMax ?? 4,
            MinOTMinutes: d.otMin ?? 50,
            WeeklyOffOTRule: d.woOT || 'Not Applicable',
            HolidayOTRule: d.hOT || 'Not Applicable',
            Wef: `${effDate}T00:00:00`,
            IsActiveStatus: d.status || 'Active',
            IsActive: (d.status || 'Active') === 'Active',
            Remarks: d.rem || ''
        };
    }

    // Expose globally
    window.formatDateDDMMYYYY = formatDateDDMMYYYY;
    window.toIsoDateStr = toIsoDateStr;
    window.renderModalDetailsGrid = renderModalDetailsGridOverride;
    window.populateDetailModalFields = populateDetailModalFieldsOverride;
    window.setDetailModalReadonly = setDetailModalReadonlyOverride;
    window.toIsoDateTime = toIsoDateTimeOverride;
    window.mapDetailToBackendDto = mapDetailToBackendDtoOverride;
    window.detailMatchesTopFilter = detailMatchesTopFilter;
    window.hasActiveDetailTopFilter = hasActiveDetailTopFilter;
    window.getFilteredDetails = getFilteredDetails;
    window.filtered = filteredOverride;
    window.render = renderOverride;
    window.kf = kfOverride;
    window.resetF = resetFOverride;
    window.previewPdfReport = previewPdfReport;
    window.closePdfPreviewModal = closePdfPreviewModal;
    window.downloadPdfReport = downloadPdfReport;
    window.exportExcelReport = exportExcelReport;
    window.exportCSV = exportExcelReport;

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && window.q('#ovPdfPreview')?.classList.contains('open')) {
            closePdfPreviewModal();
        }
    });

    // Re-render once loaded if shifts array is already present
    if (typeof shifts !== 'undefined' && Array.isArray(shifts) && shifts.length > 0) {
        renderOverride();
    }
})();
