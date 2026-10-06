// ═══════════════════════════════════════════════════════════════════
// File: wwwroot/js/pages/JobCardReport.js
// ═══════════════════════════════════════════════════════════════════
(function ($) {
    $.jobCardReport = function (options) {
        var settings = $.extend({ baseUrl: "/" }, options);

        var GetSummaryUrl = settings.baseUrl + "/GetSummary";
        var DownloadExcelUrl = settings.baseUrl + "/DownloadExcel";
        var DownloadCsvUrl = settings.baseUrl + "/DownloadCsv";

        // ── Loading Overlay ──────────────────────────────────────────
        (function setupOverlay() {
            if ($("#customLoadingOverlay").length === 0) {
                $("body").append(`
                    <div id="customLoadingOverlay" style="
                        display:none;position:fixed;top:0;left:0;
                        width:100%;height:100%;background:rgba(0,0,0,.5);
                        z-index:9999;justify-content:center;align-items:center;">
                        <div style="background:#fff;padding:20px;border-radius:5px;
                                    box-shadow:0 0 10px rgba(0,0,0,.3);text-align:center;">
                            <div class="spinner-border text-primary" role="status">
                                <span class="sr-only">Loading...</span>
                            </div>
                            <p style="margin-top:10px;margin-bottom:0;">Loading data...</p>
                        </div>
                    </div>`);
            }
        })();

        function showLoading() { $("#customLoadingOverlay").css("display", "flex"); }
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
        var currentYear = new Date().getFullYear();
        var defaultFromDate = new Date(currentYear, 0, 1); // 01/01/YYYY
        var defaultToDate = new Date(); // Today

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

        // ── Data Loader ──────────────────────────────────────────────
        function loadData(callback) {
            showLoading();
            $.ajax({
                url: GetSummaryUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                success: function (res) {
                    hideLoading();
                    if (res.success && res.data) {
                        callback(res.data);
                    } else {
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

        function getImageBase64(url, cb) {
            var img = new Image();
            img.crossOrigin = 'Anonymous';
            img.onload = function () {
                var c = document.createElement('canvas');
                c.width = img.naturalWidth;
                c.height = img.naturalHeight;
                c.getContext('2d').drawImage(img, 0, 0);
                cb(c.toDataURL('image/png'), img.naturalWidth, img.naturalHeight);
            };
            img.onerror = function () { cb(null, 0, 0); };
            img.src = url;
        }

        function getStatusColor(status) {
            var s = (status || '').trim();
            if (s === 'P') return [40, 167, 69];    // Green
            if (s === 'L') return [200, 145, 8];    // Dark Orange / Amber
            if (s === 'A') return [220, 53, 69];    // Red
            return [0, 0, 0];                       // Default Black for W, H, CL, etc.
        }

        // ── PDF Builder (Matches user's screenshots exactly) ─────────
        function buildPdf(data, forPreview) {
            var employeeGroups = data.employeeGroups || [];
            if (employeeGroups.length === 0 && data.rows && data.rows.length > 0) {
                // Group by employee
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
                alert("No records found for the selected filter.");
                return;
            }

            getImageBase64('/images/DP_logo.png', function (logoBase64, logoW, logoH) {
                var { jsPDF } = window.jspdf;
                // Portrait A4
                var doc = new jsPDF({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: 'a4'
                });

                var PW = doc.internal.pageSize.getWidth();
                var PH = doc.internal.pageSize.getHeight();
                var LEFT_MARGIN = 20;
                var RIGHT_MARGIN = 20;
                var usableWidth = PW - LEFT_MARGIN - RIGHT_MARGIN;
                var printDT = formatPrintDT();

                var companyName = (data.header && data.header.companyName) || 'DataPath Ltd.';
                var dateRange = (data.header && data.header.dateRangeDisplay) || ('Date: ' + $("#fromDateSelect").val() + ' - ' + $("#toDateSelect").val());

                function drawHeader() {
                    // Logo at Top-Left
                    if (logoBase64) {
                        try {
                            var aspect = logoH > 0 ? logoW / logoH : 2.5;
                            var renderH = 22;
                            var renderW = renderH * aspect;
                            doc.addImage(logoBase64, 'PNG', LEFT_MARGIN, 22, renderW, renderH);
                        } catch (e) { }
                    }

                    // Company Name (Centered)
                    doc.setFont('times', 'bold');
                    doc.setFontSize(12);
                    doc.setTextColor(0, 0, 0);
                    doc.text(companyName, PW / 2, 28, { align: 'center' });

                    // Report Subtitle (Centered)
                    doc.setFont('times', 'bold');
                    doc.setFontSize(9.5);
                    doc.text('Job Card Report', PW / 2, 42, { align: 'center' });

                    // Date Range (Centered)
                    doc.setFont('times', 'normal');
                    doc.setFontSize(8.5);
                    doc.text(dateRange, PW / 2, 55, { align: 'center' });
                }

                function drawFooter(pageNo) {
                    var footerY = PH - 18;
                    doc.setFont('times', 'normal');
                    doc.setFontSize(7.5);
                    doc.setTextColor(0, 0, 0);

                    // Print Datetime (Left)
                    doc.text('Print Datetime: ' + printDT, LEFT_MARGIN, footerY);

                    // Page X of Y (Right)
                    var pageStr = 'Page ' + pageNo + ' of {total_pages_count_string}';
                    doc.text(pageStr, PW + RIGHT_MARGIN+40, footerY, { align: 'right' });
                }

                function drawEmployeeInfoBlock(emp, startY) {
                    doc.setFont('times', 'normal');
                    doc.setFontSize(8);
                    doc.setTextColor(0, 0, 0);

                    var labelX = LEFT_MARGIN;
                    var colonX = labelX + 42;
                    var valX = colonX + 3;

                    // Row 1: Emp. ID
                    doc.setFont('times', 'bold');
                    doc.text('Emp. ID', labelX, startY);
                    doc.text(':', colonX, startY);
                    doc.setFont('times', 'normal');
                    doc.text(emp.employeeId || '', valX, startY);

                    // Row 2: Name
                    doc.setFont('times', 'bold');
                    doc.text('Name', labelX, startY + 12);
                    doc.text(':', colonX, startY + 12);
                    doc.setFont('times', 'normal');
                    doc.text(emp.employeeName || '', valX, startY + 12);

                    // Row 3: Designation
                    doc.setFont('times', 'bold');
                    doc.text('Designation', labelX, startY + 24);
                    doc.text(':', colonX, startY + 24);
                    doc.setFont('times', 'normal');
                    doc.text(emp.designation || '', valX, startY + 24);

                    // Row 4: Department
                    doc.setFont('times', 'bold');
                    doc.text('Department', labelX, startY + 36);
                    doc.text(':', colonX, startY + 36);
                    doc.setFont('times', 'normal');
                    doc.text(emp.departmentName || '', valX, startY + 36);

                    return startY + 44;
                }

                var isFirstEmployee = true;

                employeeGroups.forEach(function (emp) {
                    if (!isFirstEmployee) {
                        doc.addPage();
                    }
                    isFirstEmployee = false;

                    var tableStartY = drawEmployeeInfoBlock(emp, 72);

                    var bodyRows = (emp.rows || []).map(function (r) {
                        return [
                            r.dateDisplay || '',
                            r.day || '',
                            r.shift || '',
                            r.inTime || '',
                            r.late || '00:00:00',
                            r.outTime || '',
                            r.earlyOut || '00:00:00',
                            r.workHours || '00:00:00',
                            r.status || '',
                            r.remarks || ''
                        ];
                    });

                    doc.autoTable({
                        startY: tableStartY,
                        tableWidth: usableWidth,
                        margin: { left: LEFT_MARGIN, right: RIGHT_MARGIN, top: 65, bottom: 28 },
                        theme: 'grid',
                        head: [[
                            'Date',
                            'Day',
                            'Shift',
                            'In Time',
                            'Late',
                            'Out Time',
                            'Early Out',
                            'W. Hour(s)',
                            'Status',
                            'Remarks'
                        ]],
                        body: bodyRows,
                        styles: {
                            font: 'times',
                            fontSize: 6.5,
                            cellPadding: { top: 2.2, right: 2, bottom: 2.2, left: 2 },
                            halign: 'center',
                            valign: 'middle',
                            textColor: [0, 0, 0],
                            lineColor: [210, 210, 210],
                            lineWidth: 0.25,
                            fillColor: [255, 255, 255]
                        },
                        headStyles: {
                            font: 'times',
                            fontStyle: 'bold',
                            halign: 'center',
                            valign: 'middle',
                            fontSize: 7,
                            textColor: [0, 0, 0],
                            lineColor: [180, 180, 180],
                            lineWidth: 0.25,
                            fillColor: [255, 255, 255]
                        },
                        columnStyles: {
                            0: { cellWidth: 46 }, // Date
                            1: { cellWidth: 46 }, // Day
                            2: { cellWidth: 105 }, // Shift
                            3: { cellWidth: 62 }, // In Time
                            4: { cellWidth: 46 }, // Late
                            5: { cellWidth: 62 }, // Out Time
                            6: { cellWidth: 46 }, // Early Out
                            7: { cellWidth: 48 }, // W. Hour(s)
                            8: { cellWidth: 32 }, // Status
                            9: { cellWidth: 'auto', halign: 'left' } // Remarks
                        },
                        didParseCell: function (cellData) {
                            if (cellData.section === 'body') {
                                if (cellData.column.index === 9) {
                                    cellData.cell.styles.halign = 'left';
                                }
                            }
                        },
                        willDrawCell: function (cellData) {
                            if (cellData.section === 'body' && cellData.column.index === 8) {
                                var statusVal = cellData.cell.raw;
                                var color = getStatusColor(statusVal);
                                doc.setTextColor(color[0], color[1], color[2]);
                                cellData.cell.styles.fontStyle = 'bold';
                            }
                        },
                        didDrawPage: function () {
                            drawHeader();
                            drawFooter(doc.internal.getCurrentPageInfo().pageNumber);
                        }
                    });

                    // ── Bottom Summary & Stats Section (Fixed in Footer area of Employee's Last Page) ──
                    var boxH = 62;
                    var boxTopY = PH - 130;

                    // If table on this page reached into the footer area, add a page
                    if (doc.lastAutoTable.finalY > boxTopY - 10) {
                        doc.addPage();
                        drawHeader();
                        drawFooter(doc.internal.getCurrentPageInfo().pageNumber);
                    }

                    // Calculate stats for this employee
                    var totalRows = (emp.rows || []).length;
                    var presentCount = 0;
                    var lateCount = 0;
                    var absentCount = 0;
                    var weekendCount = 0;
                    var holidayCount = 0;
                    var leaveCount = 0;
                    var earlyOutCount = 0;

                    var totalLateSec = 0;
                    var totalWorkSec = 0;

                    (emp.rows || []).forEach(function (r) {
                        var st = (r.status || '').trim();
                        var lt = (r.late || '').trim();
                        var eo = (r.earlyOut || '').trim();
                        var wh = (r.workHours || '').trim();

                        if (st === 'P') {
                            presentCount++;
                        } else if (st === 'L') {
                            lateCount++;
                        } else if (st === 'A') {
                            absentCount++;
                        } else if (st === 'W') {
                            weekendCount++;
                        } else if (st === 'H') {
                            holidayCount++;
                        } else {
                            leaveCount++;
                        }

                        if (lt && lt !== '00:00:00') {
                            var lp = lt.split(':');
                            if (lp.length === 3) {
                                totalLateSec += (parseInt(lp[0], 10) * 3600) + (parseInt(lp[1], 10) * 60) + parseInt(lp[2], 10);
                            }
                        }

                        if (eo && eo !== '00:00:00') {
                            earlyOutCount++;
                        }

                        if (wh && wh !== '00:00:00') {
                            var wp = wh.split(':');
                            if (wp.length === 3) {
                                totalWorkSec += (parseInt(wp[0], 10) * 3600) + (parseInt(wp[1], 10) * 60) + parseInt(wp[2], 10);
                            }
                        }
                    });

                    function formatDurationStr(sec) {
                        var h = Math.floor(sec / 3600);
                        var rem = sec % 3600;
                        var m = Math.floor(rem / 60);
                        var s = rem % 60;
                        return h + ':' + String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0');
                    }

                    var totalPresent = presentCount + lateCount;
                    var totalWorkingDays = totalRows - weekendCount - holidayCount;
                    var totalAtt = totalPresent + weekendCount + holidayCount + leaveCount;

                    var totalLateStr = formatDurationStr(totalLateSec);
                    var totalWorkStr = formatDurationStr(totalWorkSec);

                    var avgTimeStr = "0:00";
                    if (totalPresent > 0) {
                        var avgSec = Math.round(totalWorkSec / totalPresent);
                        var avgH = Math.floor(avgSec / 3600);
                        var avgM = Math.floor((avgSec % 3600) / 60);
                        avgTimeStr = avgH + ':' + String(avgM).padStart(2, '0');
                    }

                    // 1. Draw Border around Attendance Summary Box
                    var boxX = LEFT_MARGIN;
                    var boxW = 194;

                    doc.setDrawColor(180, 180, 180);
                    doc.setLineWidth(0.5);
                    doc.rect(boxX, boxTopY, boxW, boxH);

                    // 2. Attendance Summary Statistics Text Inside Border
                    doc.setFontSize(7);
                    doc.setTextColor(0, 0, 0);

                    // Row 1 (boxTopY + 11)
                    doc.setFont('times', 'bold');
                    doc.text('Total Working Days', boxX + 6, boxTopY + 11);
                    doc.setFont('times', 'normal');
                    doc.text(String(totalWorkingDays), boxX + 90, boxTopY + 11, { align: 'right' });

                    doc.setFont('times', 'bold');
                    doc.text('Total Weekend:', boxX + 100, boxTopY + 11);
                    doc.setFont('times', 'normal');
                    doc.text(String(weekendCount), boxX + 186, boxTopY + 11, { align: 'right' });

                    // Row 2 (boxTopY + 22)
                    doc.setFont('times', 'bold');
                    doc.text('Total Present:', boxX + 25, boxTopY + 22);
                    doc.setFont('times', 'normal');
                    doc.text(String(totalPresent), boxX + 90, boxTopY + 22, { align: 'right' });

                    doc.setFont('times', 'bold');
                    doc.text('Total Holiday:', boxX + 105, boxTopY + 22);
                    doc.setFont('times', 'normal');
                    doc.text(String(holidayCount), boxX + 186, boxTopY + 22, { align: 'right' });

                    // Row 3 (boxTopY + 33)
                    doc.setFont('times', 'bold');
                    doc.text('Total Absent:', boxX + 26, boxTopY + 33);
                    doc.setFont('times', 'normal');
                    doc.text(String(absentCount), boxX + 90, boxTopY + 33, { align: 'right' });

                    doc.setFont('times', 'bold');
                    doc.text('Total Early Out:', boxX + 98, boxTopY + 33);
                    doc.setFont('times', 'normal');
                    doc.text(String(earlyOutCount), boxX + 186, boxTopY + 33, { align: 'right' });

                    // Row 4 (boxTopY + 44)
                    doc.setFont('times', 'bold');
                    doc.text('Total Late:', boxX + 34, boxTopY + 44);
                    doc.setFont('times', 'normal');
                    doc.text(String(lateCount), boxX + 90, boxTopY + 44, { align: 'right' });

                    doc.setFont('times', 'bold');
                    doc.text('Total Att.:', boxX + 117, boxTopY + 44);
                    doc.setFont('times', 'normal');
                    doc.text(String(totalAtt), boxX + 186, boxTopY + 44, { align: 'right' });

                    // Row 5 (boxTopY + 55)
                    doc.setFont('times', 'bold');
                    doc.text('Total Leave:', boxX + 30, boxTopY + 55);
                    doc.setFont('times', 'normal');
                    doc.text(String(leaveCount), boxX + 90, boxTopY + 55, { align: 'right' });

                    // 3. Column Totals Beside the Box
                    doc.setFont('times', 'bold');
                    doc.setFontSize(8);
                    doc.setTextColor(0, 0, 0);

                    // Under column Late (center X ~302) on Row 1
                    doc.text(totalLateStr, 302, boxTopY + 11, { align: 'center' });

                    // Under column W. Hour(s) (center X ~457) on Row 1
                    doc.text(totalWorkStr, 457, boxTopY + 11, { align: 'center' });

                    // Average Time on Row 4
                    doc.text('Average Time:     ' + avgTimeStr, 380, boxTopY + 44);

                    // 4. Signature Block (Above Footer)
                    var sigLineY = PH - 58;
                    var sigTextY = PH - 48;

                    doc.setDrawColor(0, 0, 0);
                    doc.setLineWidth(0.6);

                    // Prepared by (Left)
                    doc.line(LEFT_MARGIN, sigLineY, LEFT_MARGIN + 95, sigLineY);
                    doc.setFont('times', 'normal');
                    doc.setFontSize(7.5);
                    doc.text('Prepared by', LEFT_MARGIN + 47.5, sigTextY, { align: 'center' });

                    // Checked by (Center)
                    doc.line(PW / 2 - 47.5, sigLineY, PW / 2 + 47.5, sigLineY);
                    doc.text('Checked by', PW / 2, sigTextY, { align: 'center' });

                    // Authorized by (Right)
                    doc.line(PW - RIGHT_MARGIN - 95, sigLineY, PW - RIGHT_MARGIN, sigLineY);
                    doc.text('Authorized by', PW - RIGHT_MARGIN - 47.5, sigTextY, { align: 'center' });

                    // 5. Status Legend
                    var legendLine1Y = PH - 35;
                    var legendLine2Y = PH - 27;

                    doc.setFont('times', 'normal');
                    doc.setFontSize(6.5);
                    doc.setTextColor(0, 0, 0);
                    doc.text('Status Legend: P-Present, L- Late, A- Absent, W- Weekend, H- Holiday, CL- Casual Leave, SL- Sick Leave, UL- Unpaid Leave, ML- Maternity Leave,', PW / 2, legendLine1Y, { align: 'center' });
                    doc.text('PL- Paternity Leave, MarL- Marriage Leave, HL- Hajj Leave, UmrL- Umrah Leave', PW / 2, legendLine2Y, { align: 'center' });
                });

                if (typeof doc.putTotalPages === 'function') {
                    doc.putTotalPages('{total_pages_count_string}');
                }

                var fileName = 'JobCardReport_' + new Date().toISOString().slice(0, 10).replace(/-/g, '') + '.pdf';

                if (forPreview) {
                    var blob = doc.output('blob');
                    var url = URL.createObjectURL(blob);
                    $('#pdf-preview-container')
                        .html('<iframe src="' + url + '" width="100%" height="100%" style="border:none;"></iframe>')
                        .show();

                    $('html, body').animate({
                        scrollTop: $("#pdf-preview-container").offset().top - 40
                    }, 400);
                } else {
                    doc.save(fileName);
                }
            });
        }

        // ── Excel Download ───────────────────────────────────────────
        function downloadExcel() {
            showLoading();
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
            showLoading();
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
