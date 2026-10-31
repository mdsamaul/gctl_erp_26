// ═══════════════════════════════════════════════════════════════════
// File: wwwroot/js/pages/JobCardReport.js
// ═══════════════════════════════════════════════════════════════════
(function ($) {
    $.jobCardReport = function (options) {
        var settings = $.extend({ baseUrl: "/JobCardReport" }, options);
        var baseUrl = (settings.baseUrl || "/JobCardReport").replace(/\/Index\/?$/i, "").replace(/\/+$/, "");
        if (!baseUrl) baseUrl = "/JobCardReport";

        var GetSummaryUrl = baseUrl + "/GetSummary";
        var DownloadExcelUrl = baseUrl + "/DownloadExcel";
        var DownloadCsvUrl = baseUrl + "/DownloadCsv";

        // ── Loading Overlay ──────────────────────────────────────────
        (function setupOverlay() {
            if ($("#customLoadingOverlay").length === 0) {
                $("body").append(`
                    <div id="customLoadingOverlay" style="
                        display:none;position:fixed;top:0;left:0;
                        width:100%;height:100%;background:rgba(15,23,42,0.65);
                        backdrop-filter:blur(4px);-webkit-backdrop-filter:blur(4px);
                        z-index:999999;justify-content:center;align-items:center;">
                        <div style="background:#fff;padding:24px 36px;border-radius:10px;
                                    box-shadow:0 10px 25px rgba(0,0,0,0.25);text-align:center;min-width:220px;">
                            <div class="spinner-border text-primary" style="width:3rem;height:3rem;" role="status">
                                <span class="sr-only">Loading...</span>
                            </div>
                            <h6 id="jobCardLoadingMsg" style="margin-top:14px;margin-bottom:0;color:#1e293b;font-weight:600;">Processing Job Card Report...</h6>
                            <small style="color:#64748b;">Please wait a moment</small>
                        </div>
                    </div>`);
            }
        })();

        function showLoading(msg) {
            $("#jobCardLoadingMsg").text(msg || "Processing Job Card Report...");
            $("#customLoadingOverlay").css("display", "flex");
        }
        function hideLoading() { $("#customLoadingOverlay").hide(); }

        // ── Filter Builder ───────────────────────────────────────────
        function getFilter() {
            function getMultiVal(sel) {
                var v = $(sel).val();
                if (!v) return null;
                var arr = Array.isArray(v) ? v : [v];
                arr = arr.filter(function (x) { return x && x !== ""; });
                return arr.length > 0 ? arr : null;
            }

            var compVal = $("#companySelect").val();
            var compCode = Array.isArray(compVal) ? (compVal.length > 0 ? compVal[0] : null) : (compVal || null);

            return {
                CompanyCode: compCode,
                EmployeeIds: getMultiVal("#employeeSelect"),
                FromDate: $("#fromDateSelect").val() || null,
                ToDate: $("#toDateSelect").val() || null
            };
        }

        // ── Date Pickers ─────────────────────────────────────────────
        var now = new Date();
        var defaultFromDate = new Date(now.getFullYear(), now.getMonth(), 1);
        var defaultToDate = new Date();

        flatpickr("#fromDateSelect", {
            dateFormat: "d/m/Y",
            defaultDate: defaultFromDate,
            allowInput: true
        });

        flatpickr("#toDateSelect", {
            dateFormat: "d/m/Y",
            defaultDate: defaultToDate,
            allowInput: true
        });

        // ── Cache Logo Base64 Once on Init for Instant PDF Generation ──
        var cachedLogo = { loaded: false, base64: null, w: 0, h: 0 };
        function ensureLogoLoaded(cb) {
            if (cachedLogo.loaded) {
                cb(cachedLogo.base64, cachedLogo.w, cachedLogo.h);
                return;
            }
            var img = new Image();
            img.crossOrigin = 'Anonymous';
            img.onload = function () {
                try {
                    var c = document.createElement('canvas');
                    c.width = img.naturalWidth;
                    c.height = img.naturalHeight;
                    c.getContext('2d').drawImage(img, 0, 0);
                    cachedLogo.base64 = c.toDataURL('image/png');
                    cachedLogo.w = img.naturalWidth;
                    cachedLogo.h = img.naturalHeight;
                } catch (e) { }
                cachedLogo.loaded = true;
                cb(cachedLogo.base64, cachedLogo.w, cachedLogo.h);
            };
            img.onerror = function () {
                cachedLogo.loaded = true;
                cb(null, 0, 0);
            };
            img.src = '/images/DP_logo.png';
        }
        // Preload logo immediately
        ensureLogoLoaded(function () { });

        // ── Data Loader ──────────────────────────────────────────────
        function loadData(callback) {
            showLoading("Fetching Job Card Report data...");
            $.ajax({
                url: GetSummaryUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                success: function (res) {
                    if (res.success && res.data) {
                        callback(res.data);
                    } else {
                        hideLoading();
                        alert(res.message || 'No data found.');
                    }
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data.');
                }
            });
        }

        // ── Helpers ──────────────────────────────────────────────────
        function formatPrintDT() {
            var d = new Date();
            var dd = String(d.getDate()).padStart(2, '0');
            var mm = String(d.getMonth() + 1).padStart(2, '0');
            var yy = d.getFullYear();
            var h = d.getHours();
            var min = String(d.getMinutes()).padStart(2, '0');
            var sec = String(d.getSeconds()).padStart(2, '0');
            var ap = h >= 12 ? 'PM' : 'AM';
            h = h % 12 || 12;
            return dd + '/' + mm + '/' + yy + ' ' + String(h).padStart(2, '0') + ':' + min + ':' + sec + ' ' + ap;
        }

        function cleanZero(val) {
            if (val === null || val === undefined) return '';
            var s = String(val).trim();
            if (s === '0' || s === '0.0' || s === '0.00' || s === '00:00:00' || s === '0:00:00' || s === '00:00' || s === '0:00' || s === '--:--' || s === '--:--:--') {
                return '';
            }
            return s;
        }

        function getStatusColor(status) {
            var s = (status || '').trim();
            if (s === 'P') return [40, 167, 69];    // Green
            if (s === 'L') return [200, 145, 8];    // Dark Orange / Amber
            if (s === 'A') return [220, 53, 69];    // Red
            return [0, 0, 0];                       // Default Black for W, H, CL, etc.
        }

        // ── Ultra-Fast Direct A4 PDF Builder (No AutoTable/putTotalPages overhead) ──
        function buildPdf(data, forPreview) {
            var employeeGroups = data.employeeGroups || [];
            if (employeeGroups.length === 0 && data.rows && data.rows.length > 0) {
                var map = {};
                data.rows.forEach(function (r) {
                    if (!map[r.employeeId]) {
                        map[r.employeeId] = {
                            employeeId: r.employeeId,
                            employeeName: r.employeeName,
                            designation: r.designation,
                            departmentName: r.departmentName,
                            companyName: r.companyName,
                            rows: []
                        };
                        employeeGroups.push(map[r.employeeId]);
                    }
                    map[r.employeeId].rows.push(r);
                });
            }

            if (employeeGroups.length === 0) {
                hideLoading();
                alert("No records found for the selected filter.");
                return;
            }

            showLoading("Generating PDF report...");

            setTimeout(function () {
                ensureLogoLoaded(function (logoBase64, logoW, logoH) {
                    try {
                        var { jsPDF } = window.jspdf;
                        var doc = new jsPDF({
                            orientation: 'portrait',
                            unit: 'pt',
                            format: 'a4',
                            compress: true
                        });

                        var PW = doc.internal.pageSize.getWidth();   // 595.28pt
                        var PH = doc.internal.pageSize.getHeight();  // 841.89pt
                        var LEFT_MARGIN = 20;
                        var RIGHT_MARGIN = 20;
                        var usableWidth = PW - LEFT_MARGIN - RIGHT_MARGIN; // 555.28pt
                        var printDT = formatPrintDT();

                        var companyName = (data.header && data.header.companyName) || 'DataPath Ltd.';
                        var dateRange = (data.header && data.header.dateRangeDisplay) || ('Date: ' + $("#fromDateSelect").val() + ' - ' + $("#toDateSelect").val());

                        // Column definitions matching exact layout: [52, 52, 90, 55, 48, 55, 52, 36, remaining]
                        var colWidths = [52, 52, 90, 55, 48, 55, 52, 36, usableWidth - 440];
                        var colX = [];
                        var curX = LEFT_MARGIN;
                        for (var c = 0; c < colWidths.length; c++) {
                            colX.push(curX);
                            curX += colWidths[c];
                        }
                        var headers = ['Date', 'Day', 'Shift', 'In Time', 'Late', 'Out Time', 'W. Hour(s)', 'Status', 'Remarks'];

                        function drawHeader() {
                            if (logoBase64) {
                                try {
                                    var aspect = logoH > 0 ? logoW / logoH : 2.5;
                                    var renderH = 22;
                                    var renderW = renderH * aspect;
                                    doc.addImage(logoBase64, 'PNG', LEFT_MARGIN, 22, renderW, renderH, 'DP_LOGO', 'FAST');
                                } catch (e) { }
                            }

                            doc.setFont('times', 'bold');
                            doc.setFontSize(12);
                            doc.setTextColor(0, 0, 0);
                            doc.text(companyName, PW / 2, 28, { align: 'center' });

                            doc.setFont('times', 'bold');
                            doc.setFontSize(9.5);
                            doc.text('Attendance Job Card Report', PW / 2, 42, { align: 'center' });

                            doc.setFont('times', 'normal');
                            doc.setFontSize(8.5);
                            doc.text(dateRange, PW / 2, 55, { align: 'center' });
                        }

                        function drawFooter(pageNo, totalPages) {
                            var footerY = PH - 18;
                            doc.setFont('times', 'normal');
                            doc.setFontSize(7.5);
                            doc.setTextColor(0, 0, 0);

                            doc.text('Print Datetime: ' + printDT, LEFT_MARGIN, footerY);
                            doc.text('Page ' + pageNo + ' of ' + totalPages, PW - RIGHT_MARGIN, footerY, { align: 'right' });
                        }

                        function drawEmployeeInfoBlock(emp, startY) {
                            doc.setFontSize(8);
                            doc.setTextColor(0, 0, 0);

                            var labelX = LEFT_MARGIN;
                            var colonX = labelX + 42;
                            var valX = colonX + 3;

                            doc.setFont('times', 'bold');
                            doc.text('Emp. ID', labelX, startY);
                            doc.text(':', colonX, startY);
                            doc.setFont('times', 'normal');
                            doc.text(String(emp.employeeId || ''), valX, startY);

                            doc.setFont('times', 'bold');
                            doc.text('Name', labelX, startY + 12);
                            doc.text(':', colonX, startY + 12);
                            doc.setFont('times', 'normal');
                            doc.text(String(emp.employeeName || ''), valX, startY + 12);

                            doc.setFont('times', 'bold');
                            doc.text('Designation', labelX, startY + 24);
                            doc.text(':', colonX, startY + 24);
                            doc.setFont('times', 'normal');
                            doc.text(String(emp.designation || ''), valX, startY + 24);

                            doc.setFont('times', 'bold');
                            doc.text('Department', labelX, startY + 36);
                            doc.text(':', colonX, startY + 36);
                            doc.setFont('times', 'normal');
                            doc.text(String(emp.departmentName || ''), valX, startY + 36);

                            return startY + 44;
                        }

                        function drawFastTable(rows, startY) {
                            var headH = 16;
                            var rowH = 15.1;
                            var curY = startY;

                            // Draw Table Header
                            doc.setDrawColor(180, 180, 180);
                            doc.setLineWidth(0.25);
                            doc.rect(LEFT_MARGIN, curY, usableWidth, headH);

                            doc.setFont('times', 'bold');
                            doc.setFontSize(8);
                            doc.setTextColor(0, 0, 0);
                            var headTextY = curY + 10.8;
                            for (var c = 0; c < headers.length; c++) {
                                if (c > 0) {
                                    doc.line(colX[c], curY, colX[c], curY + headH);
                                }
                                doc.text(headers[c], colX[c] + colWidths[c] / 2, headTextY, { align: 'center' });
                            }
                            curY += headH;

                            // Draw Body Rows
                            doc.setDrawColor(210, 210, 210);
                            doc.setLineWidth(0.25);
                            var bodyStartY = curY;
                            var numRows = rows.length;
                            var totalBodyH = numRows * rowH;

                            // Outer border of body
                            if (numRows > 0) {
                                doc.rect(LEFT_MARGIN, bodyStartY, usableWidth, totalBodyH);
                                // Vertical column lines once for the entire table body!
                                for (var vc = 1; vc < colWidths.length; vc++) {
                                    doc.line(colX[vc], bodyStartY, colX[vc], bodyStartY + totalBodyH);
                                }
                            }

                            doc.setFontSize(7.5);
                            for (var rIdx = 0; rIdx < numRows; rIdx++) {
                                var r = rows[rIdx];
                                if (rIdx > 0) {
                                    doc.line(LEFT_MARGIN, curY, LEFT_MARGIN + usableWidth, curY);
                                }
                                var textY = curY + 10.2;

                                doc.setFont('times', 'normal');
                                doc.setTextColor(0, 0, 0);

                                if (r.dateDisplay) doc.text(String(r.dateDisplay), colX[0] + colWidths[0] / 2, textY, { align: 'center' });
                                if (r.day) doc.text(String(r.day), colX[1] + colWidths[1] / 2, textY, { align: 'center' });
                                if (r.shift) doc.text(String(r.shift), colX[2] + colWidths[2] / 2, textY, { align: 'center' });

                                var inT = cleanZero(r.inTime);
                                if (inT) doc.text(inT, colX[3] + colWidths[3] / 2, textY, { align: 'center' });

                                var lateT = cleanZero(r.late);
                                if (lateT) doc.text(lateT, colX[4] + colWidths[4] / 2, textY, { align: 'center' });

                                var outT = cleanZero(r.outTime);
                                if (outT) doc.text(outT, colX[5] + colWidths[5] / 2, textY, { align: 'center' });

                                var whT = cleanZero(r.workHours);
                                if (whT) doc.text(whT, colX[6] + colWidths[6] / 2, textY, { align: 'center' });

                                var stVal = (r.status || '').trim();
                                if (stVal) {
                                    var stColor = getStatusColor(stVal);
                                    doc.setFont('times', 'bold');
                                    doc.setTextColor(stColor[0], stColor[1], stColor[2]);
                                    doc.text(stVal, colX[7] + colWidths[7] / 2, textY, { align: 'center' });
                                    doc.setFont('times', 'normal');
                                    doc.setTextColor(0, 0, 0);
                                }

                                var rem = (r.remarks || '').trim();
                                if (rem) {
                                    // Truncate remarks cleanly if wider than Remarks column
                                    if (rem.length > 34) rem = rem.substring(0, 34);
                                    doc.text(rem, colX[8] + 3, textY);
                                }

                                curY += rowH;
                            }

                            return curY;
                        }

                        var totalEmps = employeeGroups.length;

                        for (var idx = 0; idx < totalEmps; idx++) {
                            var emp = employeeGroups[idx];
                            if (idx > 0) {
                                doc.addPage();
                            }

                            drawHeader();
                            var tableStartY = drawEmployeeInfoBlock(emp, 72);
                            var finalY = drawFastTable(emp.rows || [], tableStartY);

                            // ── Bottom Summary & Stats Section (starts 30px = 22.5pt after table ends) ──
                            var marginAfterTablePt = 22.5; // 30px bottom margin after 30/31 rows
                            var boxH = 65;
                            var sigBlockReserve = 68;
                            var boxTopY = finalY + marginAfterTablePt;

                            if (boxTopY + boxH > PH - sigBlockReserve) {
                                doc.addPage();
                                drawHeader();
                                boxTopY = 72;
                            }

                            // Calculate stats for this employee
                            var presentCount = 0;
                            var lateCount = 0;
                            var absentCount = 0;
                            var weekendCount = 0;
                            var holidayCount = 0;
                            var leaveCount = 0;
                            var totalWorkHrs = 0;
                            var empRows = emp.rows || [];

                            for (var ri = 0; ri < empRows.length; ri++) {
                                var r = empRows[ri];
                                var st = (r.status || '').trim();
                                var wh = (r.workHours || '').trim();

                                if (st === 'P') presentCount++;
                                else if (st === 'L') lateCount++;
                                else if (st === 'A') absentCount++;
                                else if (st === 'W') weekendCount++;
                                else if (st === 'H') holidayCount++;
                                else leaveCount++;

                                if (wh && wh !== '00:00:00' && wh !== '0.00' && wh !== '0') {
                                    if (wh.indexOf(':') !== -1) {
                                        var wp = wh.split(':');
                                        if (wp.length === 3) {
                                            totalWorkHrs += ((parseInt(wp[0], 10) * 3600) + (parseInt(wp[1], 10) * 60) + parseInt(wp[2], 10)) / 3600;
                                        } else if (wp.length === 2) {
                                            totalWorkHrs += ((parseInt(wp[0], 10) * 3600) + (parseInt(wp[1], 10) * 60)) / 3600;
                                        }
                                    } else {
                                        var decW = parseFloat(wh);
                                        if (!isNaN(decW)) {
                                            totalWorkHrs += decW;
                                        }
                                    }
                                }
                            }

                            var totalPresent = emp.totalPresent !== undefined ? emp.totalPresent : (presentCount + lateCount);
                            var totalAbsent = emp.totalAbsent !== undefined ? emp.totalAbsent : absentCount;
                            var totalLate = emp.totalLate !== undefined ? emp.totalLate : lateCount;
                            var totalLeave = emp.totalLeave !== undefined ? emp.totalLeave : leaveCount;
                            var totalWeekend = emp.totalWeekend !== undefined ? emp.totalWeekend : weekendCount;
                            var totalHoliday = emp.totalHoliday !== undefined ? emp.totalHoliday : holidayCount;
                            var totalAtt = emp.totalAtt !== undefined ? emp.totalAtt : (totalPresent + totalWeekend + totalHoliday + totalLeave);
                            var totalWorkHrsVal = emp.totalWorkHours !== undefined ? Number(emp.totalWorkHours).toFixed(2) : totalWorkHrs.toFixed(2);
                            var aveWorkHrsVal = emp.aveWorkHoursPerDay !== undefined
                                ? Number(emp.aveWorkHoursPerDay).toFixed(2)
                                : (totalPresent > 0 ? (totalWorkHrs / totalPresent).toFixed(2) : "0.00");

                            // 1. Draw Border around Attendance Summary Box
                            var boxX = LEFT_MARGIN;
                            var boxW = 265;

                            doc.setDrawColor(180, 180, 180);
                            doc.setLineWidth(0.5);
                            doc.rect(boxX, boxTopY, boxW, boxH);

                            // 2. Attendance Summary Statistics Text Inside Border
                            doc.setFontSize(7.5);
                            doc.setTextColor(0, 0, 0);

                            // Row 1
                            doc.setFont('times', 'bold');
                            doc.text('Total Present :', boxX + 70, boxTopY + 11, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(totalPresent), boxX + 75, boxTopY + 11);

                            doc.setFont('times', 'bold');
                            doc.text('Total Weekend:', boxX + 205, boxTopY + 11, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(totalWeekend), boxX + 210, boxTopY + 11);

                            // Row 2
                            doc.setFont('times', 'bold');
                            doc.text('Total Absent :', boxX + 70, boxTopY + 23, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(totalAbsent), boxX + 75, boxTopY + 23);

                            doc.setFont('times', 'bold');
                            doc.text('Total Holiday:', boxX + 205, boxTopY + 23, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(totalHoliday), boxX + 210, boxTopY + 23);

                            // Row 3
                            doc.setFont('times', 'bold');
                            doc.text('Total Late :', boxX + 70, boxTopY + 35, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(totalLate), boxX + 75, boxTopY + 35);

                            doc.setFont('times', 'bold');
                            doc.text('Total Att.:', boxX + 205, boxTopY + 35, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(totalAtt), boxX + 210, boxTopY + 35);

                            // Row 4
                            doc.setFont('times', 'bold');
                            doc.text('Total Leave :', boxX + 70, boxTopY + 47, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(totalLeave), boxX + 75, boxTopY + 47);

                            doc.setFont('times', 'bold');
                            doc.text('Ave. W. Hrs/Day.:', boxX + 205, boxTopY + 47, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(aveWorkHrsVal), boxX + 210, boxTopY + 47);

                            // Row 5
                            doc.setFont('times', 'bold');
                            doc.text('Total W. Hrs :', boxX + 70, boxTopY + 59, { align: 'right' });
                            doc.setFont('times', 'normal');
                            doc.text(String(totalWorkHrsVal), boxX + 75, boxTopY + 59);

                            // 3. Signatures
                            var sigLineY = PH - 58;
                            var sigTextY = PH - 48;

                            doc.setDrawColor(0, 0, 0);
                            doc.setLineWidth(0.6);

                            doc.line(LEFT_MARGIN, sigLineY, LEFT_MARGIN + 95, sigLineY);
                            doc.setFont('times', 'normal');
                            doc.setFontSize(7.5);
                            doc.text('Prepared by', LEFT_MARGIN + 47.5, sigTextY, { align: 'center' });

                            doc.line(PW / 2 - 47.5, sigLineY, PW / 2 + 47.5, sigLineY);
                            doc.text('Checked by', PW / 2, sigTextY, { align: 'center' });

                            doc.line(PW - RIGHT_MARGIN - 95, sigLineY, PW - RIGHT_MARGIN, sigLineY);
                            doc.text('Authorized by', PW - RIGHT_MARGIN - 47.5, sigTextY, { align: 'center' });

                            // 4. Status Legend
                            var legendLine1Y = PH - 35;
                            var legendLine2Y = PH - 27;

                            doc.setFont('times', 'normal');
                            doc.setFontSize(6.5);
                            doc.setTextColor(0, 0, 0);
                            doc.text('Status Legend: P-Present, L- Late, A- Absent, W- Weekend, H- Holiday, CL- Casual Leave, SL- Sick Leave, UL- Unpaid Leave, ML- Maternity Leave,', PW / 2, legendLine1Y, { align: 'center' });
                            doc.text('PL- Paternity Leave, MarL- Marriage Leave, HL- Hajj Leave, UmrL- Umrah Leave', PW / 2, legendLine2Y, { align: 'center' });
                        }

                        // Stamp exact page numbers in a fast O(N) pass without regex-scanning PDF streams
                        var totalPagesCount = doc.internal.getNumberOfPages();
                        for (var p = 1; p <= totalPagesCount; p++) {
                            doc.setPage(p);
                            drawFooter(p, totalPagesCount);
                        }

                        var fileName = 'JobCardReport_' + new Date().toISOString().slice(0, 10).replace(/-/g, '') + '.pdf';

                        if (forPreview) {
                            var blob = doc.output('blob');
                            var url = URL.createObjectURL(blob);
                            $('#pdf-preview-container')
                                .html('<iframe src="' + url + '" width="100%" height="100%" style="border:none;"></iframe>')
                                .show();

                            hideLoading();
                            $('html, body').animate({
                                scrollTop: $("#pdf-preview-container").offset().top - 40
                            }, 300);
                        } else {
                            doc.save(fileName);
                            hideLoading();
                        }
                    } catch (err) {
                        hideLoading();
                        console.error(err);
                        alert("Error generating PDF report.");
                    }
                });
            }, 20);
        }

        // ── Excel Download ───────────────────────────────────────────
        function downloadExcel() {
            showLoading("Exporting Excel report...");
            $.ajax({
                url: DownloadExcelUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                xhrFields: { responseType: 'blob' },
                success: function (blob, status, xhr) {
                    hideLoading();

                    if (blob.type && blob.type.indexOf('application/json') !== -1) {
                        var reader = new FileReader();
                        reader.onload = function () {
                            try {
                                var err = JSON.parse(reader.result);
                                alert(err.message || 'Excel download failed!');
                            } catch (e) {
                                alert('Excel download failed!');
                            }
                        };
                        reader.readAsText(blob);
                        return;
                    }

                    var cd = xhr.getResponseHeader('Content-Disposition');
                    var ts = new Date().toISOString().replace(/[-T:.Z]/g, '').slice(0, 15);
                    var fileName = 'JobCardReport_' + ts + '.xlsx';
                    if (cd) {
                        var match = cd.match(/filename[^;=\n]*=(['"]?)([^'";\n]+)\1/);
                        if (match && match[2]) fileName = match[2];
                    }

                    var link = document.createElement('a');
                    link.href = URL.createObjectURL(blob);
                    link.download = fileName;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    URL.revokeObjectURL(link.href);
                },
                error: function (xhr) {
                    hideLoading();
                    var msg = 'Excel download failed!';
                    try {
                        var err = JSON.parse(xhr.responseText);
                        if (err.message) msg = err.message;
                    } catch (e) { }
                    alert(msg);
                }
            });
        }

        // ── CSV Download ─────────────────────────────────────────────
        function downloadCsv() {
            showLoading("Exporting CSV report...");
            $.ajax({
                url: DownloadCsvUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                xhrFields: { responseType: 'blob' },
                success: function (blob, status, xhr) {
                    hideLoading();

                    if (blob.type && blob.type.indexOf('application/json') !== -1) {
                        var reader = new FileReader();
                        reader.onload = function () {
                            try {
                                var err = JSON.parse(reader.result);
                                alert(err.message || 'CSV download failed!');
                            } catch (e) {
                                alert('CSV download failed!');
                            }
                        };
                        reader.readAsText(blob);
                        return;
                    }

                    var cd = xhr.getResponseHeader('Content-Disposition');
                    var ts = new Date().toISOString().replace(/[-T:.Z]/g, '').slice(0, 15);
                    var fileName = 'JobCardReport_' + ts + '.csv';
                    if (cd) {
                        var match = cd.match(/filename[^;=\n]*=(['"]?)([^'";\n]+)\1/);
                        if (match && match[2]) fileName = match[2];
                    }

                    var link = document.createElement('a');
                    link.href = URL.createObjectURL(blob);
                    link.download = fileName;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    URL.revokeObjectURL(link.href);
                },
                error: function (xhr) {
                    hideLoading();
                    var msg = 'CSV download failed!';
                    try {
                        var err = JSON.parse(xhr.responseText);
                        if (err.message) msg = err.message;
                    } catch (e) { }
                    alert(msg);
                }
            });
        }

        // ── Button Event Bindings ────────────────────────────────────
        $(document).on('click', '#btnPreviewPdf', function () {
            loadData(function (data) {
                buildPdf(data, true);
            });
        });

        $(document).on('click', '#downloadReport', function () {
            var format = $("#reportFormatSelect").val();
            if (!format) {
                alert("Please select an Export Report Format first.");
                return;
            }

            if (format === "downloadPdf") {
                loadData(function (data) {
                    buildPdf(data, false);
                });
            } else if (format === "downloadExcel") {
                downloadExcel();
            } else if (format === "downloadCsv") {
                downloadCsv();
            }
        });

        // ── Initialize Remote Cascade / Multi-Select ─────────────────
        if (typeof settings.load === "function") {
            settings.load();
        }
    };
})(jQuery);
