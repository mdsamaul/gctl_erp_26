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
        var now = new Date();
        var defaultFromDate = new Date(now.getFullYear(), now.getMonth(), 1); // First day of current month (01/MM/YYYY)
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
                    doc.text('Attendance Job Card Report', PW / 2, 42, { align: 'center' });

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
                            cleanZero(r.inTime),
                            cleanZero(r.late),
                            cleanZero(r.outTime),
                            cleanZero(r.workHours),
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
                            0: { cellWidth: 52 }, // Date
                            1: { cellWidth: 52 }, // Day
                            2: { cellWidth: 90 }, // Shift
                            3: { cellWidth: 55 }, // In Time
                            4: { cellWidth: 48 }, // Late
                            5: { cellWidth: 55 }, // Out Time
                            6: { cellWidth: 52 }, // W. Hour(s)
                            7: { cellWidth: 36 }, // Status
                            8: { cellWidth: 'auto', halign: 'left' } // Remarks
                        },
                        didParseCell: function (cellData) {
                            if (cellData.section === 'body') {
                                if (cellData.column.index === 8) {
                                    cellData.cell.styles.halign = 'left';
                                }
                            }
                        },
                        willDrawCell: function (cellData) {
                            if (cellData.section === 'body' && cellData.column.index === 7) {
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

                    // ── Bottom Summary & Stats Section ───────────────────────
                    var boxH = 65;
                    var boxTopY = PH - 135;

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
                    var totalWorkHrs = 0;

                    (emp.rows || []).forEach(function (r) {
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
                    });

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

                    // 2. Attendance Summary Statistics Text Inside Border (exact match to Excel)
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
