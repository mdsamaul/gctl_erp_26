(function ($) {
    $.salaryInformationReport = function (options) {
        var settings = $.extend({
            baseUrl: '',
            load: function () { }
        }, options);

        var lastResult = [];

        function getSelectedValues(id) {
            var v = $(id).val();
            if (!v) return null;
            return Array.isArray(v) ? v.join(',') : v;
        }

        function buildFilter() {
            var generateType = $('input[name="salaryGenerate"]:checked').val();
            return {
                CompanyCode: getSelectedValues('#companySelect'),
                BranchCode: getSelectedValues('#branchSelect'),
                DepartmentCode: getSelectedValues('#departmentSelect'),
                EmployeeID: getSelectedValues('#employeeSelect'),
                ModeOfPayment: getSelectedValues('#modeOfPaymentSelect'),
                EmploymentNature: getSelectedValues('#employmentNatureSelect'),
                GenerateType: generateType,
                DateFrom: generateType === 'ByDate' ? ($('#dateFrom').val() || null) : null,
                DateTo: generateType === 'ByDate' ? ($('#dateTo').val() || null) : null,
                MonthName: generateType === 'ByMonth' ? $('#monthSelect option:selected').text() : null,
                YearName: generateType === 'ByMonth' ? parseInt($('#yearSelect').val(), 10) : null,
                AsOnDate: null,
                ExportFormat: $('#exportFormatSelect').val(),
                MasterFileType: $('#masterFileTypeSelect').val() || null
            };
        }
        function initCustomLoader() {
            if (document.getElementById('erp-custom-loader')) return;

            const style = document.createElement('style');
            style.id = 'erp-loader-style';
            style.innerHTML = `
            .erp-loader-overlay {
                position: fixed;
                inset: 0;
                background: rgba(15, 23, 42, 0.65);
                backdrop-filter: blur(6px);
                -webkit-backdrop-filter: blur(6px);
                display: flex;
                align-items: center;
                justify-content: center;
                z-index: 999999;
                opacity: 0;
                visibility: hidden;
                transition: opacity 0.25s ease, visibility 0.25s ease;
            }
            .erp-loader-overlay.active {
                opacity: 1;
                visibility: visible;
            }
            .erp-loader-card {
                background: #ffffff;
                padding: 24px 32px;
                border-radius: 12px;
                box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.2), 0 10px 10px -5px rgba(0, 0, 0, 0.08);
                display: flex;
                flex-direction: column;
                align-items: center;
                gap: 14px;
                min-width: 180px;
            }
            .erp-spinner {
                width: 44px;
                height: 44px;
                border: 4px solid #e2e8f0;
                border-top: 4px solid #2563eb;
                border-right: 4px solid #2563eb;
                border-radius: 50%;
                animation: erp-spin 0.75s cubic-bezier(0.68, -0.55, 0.27, 1.55) infinite;
            }
            .erp-loader-text {
                color: #1e293b;
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                font-size: 14px;
                font-weight: 600;
                letter-spacing: 0.3px;
            }
            @keyframes erp-spin {
                0% { transform: rotate(0deg); }
                100% { transform: rotate(360deg); }
            }
        `;
            document.head.appendChild(style);

            const overlay = document.createElement('div');
            overlay.id = 'erp-custom-loader';
            overlay.className = 'erp-loader-overlay';
            overlay.innerHTML = `
            <div class="erp-loader-card">
                <div class="erp-spinner"></div>
                <div class="erp-loader-text" id="erp-loader-msg">Processing...</div>
            </div>
        `;
            document.body.appendChild(overlay);
        }

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', initCustomLoader);
        } else {
            initCustomLoader();
        }
        function showLoading(message) {
            var loader = document.getElementById('erp-custom-loader');
            var msgEl = document.getElementById('erp-loader-msg');
            if (msgEl) {
                msgEl.innerText = message || 'Processing, please wait...';
            }
            if (loader) {
                loader.classList.add('active');
            }
        }

        function hideLoading() {
            var loader = document.getElementById('erp-custom-loader');
            if (loader) {
                loader.classList.remove('active');
            }
        }

        function toast(msg, isError) {
            if (window.toastr) {
                isError ? toastr.error(msg) : toastr.success(msg);
            } else {
                alert(msg);
            }
        }

        flatpickr("#dateFrom, #dateTo", CalendarService.createConfig({ defaultDate: new Date() }));

        function getImageBase64FromUrl(url, callback) {
            var img = new Image();
            img.crossOrigin = 'Anonymous';
            img.onload = function () {
                var canvas = document.createElement('canvas');
                canvas.width = img.naturalWidth;
                canvas.height = img.naturalHeight;
                canvas.getContext('2d').drawImage(img, 0, 0);
                callback(canvas.toDataURL('image/png'), img.naturalWidth, img.naturalHeight);
            };
            img.onerror = function () {
                callback(null, 0, 0);
            };
            img.src = url;
        }

        function formatDateDMY(dateStr) {
            if (!dateStr) return '';
            var d = new Date(dateStr);
            if (isNaN(d.getTime())) return dateStr;
            var day = String(d.getDate()).padStart(2, '0');
            var month = String(d.getMonth() + 1).padStart(2, '0');
            var year = d.getFullYear();
            return day + '/' + month + '/' + year;
        }

        function formatAmount(value) {
            if (value == null || value === '') return '';
            return Number(value).toLocaleString('en-US', {
                minimumFractionDigits: 0,
                maximumFractionDigits: 0
            });
        }

        function buildPdfDoc(filter, rows, callback) {
            
            getImageBase64FromUrl('/images/DP_logo.png', function (base64Logo, natW, natH) {
                var LOGO_TARGET_HEIGHT = 26;
                var logoWidth = (natH > 0) ? (natW / natH) * LOGO_TARGET_HEIGHT : LOGO_TARGET_HEIGHT;
                var jsPDFCtor = window.jspdf ? window.jspdf.jsPDF : window.jsPDF;
                var doc = new jsPDFCtor({
                    orientation: 'landscape',
                    unit: 'pt',
                    format: [1045, 650]
                });
                var pageWidth = doc.internal.pageSize.getWidth();
                var pageHeight = doc.internal.pageSize.getHeight();
                var leftMargin = 12;
                var rightMargin = 12;
                var topMargin = 70;
                var bottomMargin = 40;

                function drawHeader() {
                    if (base64Logo) {
                        doc.addImage(base64Logo, 'PNG', 14, 20, logoWidth, LOGO_TARGET_HEIGHT);
                    }
                    doc.setFont("times", "bold");
                    doc.setFontSize(13);
                    doc.text('DataPath Ltd.', pageWidth / 2, 22, { align: 'center' });
                    doc.setFontSize(11);
                    doc.text('Payroll Master File - General', pageWidth / 2, 38, { align: 'center' });
                    doc.setFontSize(8);
                    doc.setFont("times", "normal");
                    var periodText = filter.GenerateType === 'ByMonth'
                        ? 'For the month of ' + filter.MonthName + ', ' + filter.YearName
                        : 'For the period ' + (formatDateDMY(filter.DateFrom) || '') + ' to ' + (formatDateDMY(filter.DateTo) || '');
                    doc.text(periodText, pageWidth / 2, 52, { align: 'center' });
                }

                function drawFooter() {
                    var pageCount = doc.internal.getNumberOfPages();
                    var currentPage = doc.internal.getCurrentPageInfo().pageNumber;
                    var now = new Date();
                    var hours = now.getHours();
                    var minutes = now.getMinutes().toString().padStart(2, '0');
                    var ampm = hours >= 12 ? 'PM' : 'AM';
                    hours = hours % 12 || 12;
                    var day = now.getDate().toString().padStart(2, '0');
                    var month = (now.getMonth() + 1).toString().padStart(2, '0');
                    var year = now.getFullYear();
                    var printDateTime = day + '/' + month + '/' + year + ' ' + hours + ':' + minutes + ' ' + ampm;
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    doc.setTextColor(80);
                    doc.text('Print Datetime: ' + printDateTime, leftMargin, pageHeight - 18);
                    doc.text('GCTL Infosys - HRM & Finance System', pageWidth / 2, pageHeight - 18, { align: 'center' });
                    doc.text('Page ' + currentPage + ' of ' + pageCount, pageWidth - rightMargin, pageHeight - 18, { align: 'right' });
                    doc.setTextColor(0);
                }

                var head = [[
                    'SL', 'ID No.', 'Pay ID', 'DP User ID', 'DBBL Employee Name', 'UCBL Employee Name',
                    'Status', 'Department', 'Designation', 'DOH', 'DOT', 'Duration', 'DBBL', 'UCBL',
                    'Salary', 'Yearly Bonus Eligibility', 'Gratuity Eligibility', 'Eid Bonus Eligibility',
                    'PF Eligible', 'Gender', 'Cell Phone', 'Special Notes', 'End of Probation'
                ]];

                var body = rows.map(function (row) {
                    return [
                        row.sl, row.idNo, row.payId, row.dpUserId, row.dbblEmployeesName,
                        row.ucblEmployeesName, row.status, row.department, row.designation,
                        row.doh, row.dot, row.duration, row.dbbl, row.ucbl, row.salary,
                        row.yearlyBonusEligibility, row.gratuityEligibility, row.eidBonusEligibility,
                        row.pfEligiblity, row.gender, row.cellPhone, row.specialNotes,
                        row.endOfProbation
                    ];
                });

                var totalSalary = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.salary) || 0);
                }, 0);

                var columnStyles = {
                    0: { cellWidth: 15, halign: 'center' },
                    1: { cellWidth: 40, halign: 'center' },
                    2: { cellWidth: 25, halign: 'center' },
                    3: { cellWidth: 40, halign: 'center' },
                    4: { cellWidth: 90 },
                    5: { cellWidth: 90 },
                    6: { cellWidth: 25, halign: 'center' },
                    7: { cellWidth: 60 },
                    8: { cellWidth: 55 },
                    9: { cellWidth: 35, halign: 'center' },
                    10: { cellWidth: 35, halign: 'center' },
                    11: { cellWidth: 33, halign: 'center' },
                    12: { cellWidth: 55, halign: 'center' },
                    13: { cellWidth: 60, halign: 'center' },
                    14: { cellWidth: 45, halign: 'right' },
                    15: { cellWidth: 45, halign: 'center' },
                    16: { cellWidth: 45, halign: 'center' },
                    17: { cellWidth: 45, halign: 'center' },
                    18: { cellWidth: 32, halign: 'center' },
                    19: { cellWidth: 27, halign: 'center' },
                    20: { cellWidth: 50, halign: 'center' },
                    21: { cellWidth: 40, halign: 'center' },
                    22: { cellWidth: 35, halign: 'center' }
                };

                var footRow = [
                    {
                        content: 'Total',
                        colSpan: 14,
                        styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 }
                    },
                    {
                        content: formatAmount(totalSalary),
                        styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 }
                    }
                ];

                doc.autoTable({
                    theme: 'grid',
                    head: head,
                    body: body,
                    foot: [footRow],

                    styles: {
                        font: "times",
                        fontSize: 6.5,
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        cellPadding: 2,
                        valign: 'middle',
                        overflow: 'linebreak',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },

                    headStyles: {
                        fillColor: [54, 96, 146],
                        textColor: [255, 255, 255],
                        fontStyle: "bold",
                        font: "times",
                        fontSize: 7,
                        halign: 'center',
                        valign: 'middle',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },

                    alternateRowStyles: {
                        fillColor: [255, 255, 255]
                    },

                    bodyStyles: {
                        fillColor: [255, 255, 255]
                    },

                    footStyles: {
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        fontStyle: "bold",
                        fontSize: 7,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },

                    columnStyles: columnStyles,

                    tableWidth: 'wrap',

                    margin: {
                        top: topMargin,
                        bottom: bottomMargin,
                        left: leftMargin,
                        right: rightMargin
                    },

                    didParseCell: function (data) {
                        if (data.section === 'body' || data.section === 'foot') {
                            data.cell.styles.fillColor = [255, 255, 255];
                        }
                    },

                    didDrawPage: function () {
                        drawHeader();
                        drawFooter();
                    }
                });

                if (typeof callback === 'function') {
                    callback(doc);
                }
            });
        }

        function exportExcel(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/ExportToExcel',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                xhrFields: { responseType: 'blob' },
                success: function (data, textStatus, jqXHR) {
                    hideLoading();
                    var contentType = jqXHR.getResponseHeader('Content-Type') || '';
                    if (contentType.indexOf('application/json') !== -1 || contentType.indexOf('text/html') !== -1) {
                        var reader = new FileReader();
                        reader.onload = function () {
                            try {
                                var errObj = JSON.parse(reader.result);
                                alert(errObj.message || 'Failed to generate Excel file');
                            } catch (e) {
                                alert('Failed to generate Excel file');
                            }
                        };
                        reader.readAsText(data);
                        return;
                    }
                    if (!data || data.size === 0) {
                        alert('No data found to download');
                        return;
                    }
                    var url = window.URL.createObjectURL(data);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'PayrollMasterFile_General.xlsx';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                    window.URL.revokeObjectURL(url);
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function exportPdf(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFile',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    lastResult = res.data || [];
                    if (lastResult.length === 0) {
                        alert('No data found to download');
                        return;
                    }
                    buildPdfDoc(filter, lastResult, function (doc) {
                        doc.save('PayrollMasterFile_General.pdf');
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function previewPdf(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFile',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    lastResult = res.data || [];
                    if (lastResult.length === 0) {
                        alert('No data found to preview');
                        return;
                    }
                    buildPdfDoc(filter, lastResult, function (doc) {
                        var blobUrl = doc.output('bloburl');
                        var $container = $('#pdf-preview-container');
                        $container.empty();
                        var $iframe = $('<iframe>', {
                            src: blobUrl,
                            style: 'width:100%; height:100%; border:0;'
                        });
                        $container.append($iframe).show();
                        $container[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function exportExcelGratuity(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/ExportToExcelGratuity',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                xhrFields: { responseType: 'blob' },
                success: function (data) {
                    hideLoading();
                    if (!data || data.size === 0) {
                        alert('No data found to download');
                        return;
                    }
                    var url = window.URL.createObjectURL(data);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'PayrollMasterFile_Gratuity.xlsx';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                    window.URL.revokeObjectURL(url);
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function exportPdfGratuity(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileGratuity',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to download');
                        return;
                    }
                    buildPdfDocGratuity(filter, rows, function (doc) {
                        doc.save('PayrollMasterFile_Gratuity.pdf');
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function previewPdfGratuity(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileGratuity',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to preview');
                        return;
                    }
                    buildPdfDocGratuity(filter, rows, function (doc) {
                        var blobUrl = doc.output('bloburl');
                        var $container = $('#pdf-preview-container');
                        $container.empty();
                        var $iframe = $('<iframe>', {
                            src: blobUrl,
                            style: 'width:100%; height:100%; border:0;'
                        });
                        $container.append($iframe).show();
                        $container[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function buildPdfDocGratuity(filter, rows, callback) {
            getImageBase64FromUrl('/images/DP_logo.png', function (base64Logo, natW, natH) {
                var LOGO_TARGET_HEIGHT = 26;
                var logoWidth = (natH > 0) ? (natW / natH) * LOGO_TARGET_HEIGHT : LOGO_TARGET_HEIGHT;
                var jsPDFCtor = window.jspdf ? window.jspdf.jsPDF : window.jsPDF;
                var CUSTOM_PAGE_WIDTH = 570;
                var A4_PAGE_HEIGHT = 841.89;
                var doc = new jsPDFCtor({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: [CUSTOM_PAGE_WIDTH, A4_PAGE_HEIGHT]
                });

                var pageWidth = doc.internal.pageSize.getWidth();
                var pageHeight = doc.internal.pageSize.getHeight();
                var leftMargin = 15;
                var rightMargin = 15;
                var topMargin = 70;
                var bottomMargin = 40;
                var availableWidth = pageWidth - leftMargin - rightMargin;

                function drawHeader() {
                    if (base64Logo) {
                        doc.addImage(base64Logo, 'PNG', leftMargin, 15, logoWidth, LOGO_TARGET_HEIGHT);
                    }
                    doc.setFont("times", "bold");
                    doc.setFontSize(13);
                    doc.text('DataPath Ltd.', pageWidth / 2, 22, { align: 'center' });
                    doc.setFontSize(11);
                    doc.text('Payroll Master File - Gratuity', pageWidth / 2, 38, { align: 'center' });
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    var periodText = filter.GenerateType === 'ByMonth'
                        ? 'For the month of ' + filter.MonthName + ', ' + filter.YearName
                        : 'For the period ' + (formatDateDMY(filter.DateFrom) || '') + ' to ' + (formatDateDMY(filter.DateTo) || '');
                    doc.text(periodText, pageWidth / 2, 52, { align: 'center' });
                }

                function drawFooter() {
                    var pageCount = doc.internal.getNumberOfPages();
                    var currentPage = doc.internal.getCurrentPageInfo().pageNumber;
                    var now = new Date();
                    var hours = now.getHours();
                    var minutes = now.getMinutes().toString().padStart(2, '0');
                    var ampm = hours >= 12 ? 'PM' : 'AM';
                    hours = hours % 12 || 12;
                    var day = now.getDate().toString().padStart(2, '0');
                    var month = (now.getMonth() + 1).toString().padStart(2, '0');
                    var year = now.getFullYear();
                    var printDateTime = day + '/' + month + '/' + year + ' ' + hours + ':' + minutes + ' ' + ampm;
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    doc.setTextColor(80);
                    doc.text('Print Datetime: ' + printDateTime, leftMargin, pageHeight - 18);
                    doc.text('GCTL Infosys - HRM & Finance System', pageWidth / 2, pageHeight - 18, { align: 'center' });
                    doc.text('Page ' + currentPage + ' of ' + pageCount, pageWidth - rightMargin, pageHeight - 18, { align: 'right' });
                    doc.setTextColor(0);
                }

                var head = [[
                    'SL', 'ID NO.', 'Pay ID', 'Name of the Employee', 'Status', 'Department',
                    'DOH', 'DOT', 'Bank Account No', 'Salary', 'Tenure', 'Gratuity', 'Note'
                ]];

                var body = rows.map(function (row) {
                    return [
                        row.sl, row.idNo, row.payId, row.nameOfTheEmployee, row.status,
                        row.department, row.dateOfHire, row.dot, row.bankAccountNo,
                        formatAmount(row.salary), formatAmount(row.tenure),
                        formatAmount(row.gratuity), row.note
                    ];
                });

                var totalSalary = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.salary) || 0);
                }, 0);

                var totalGratuity = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.gratuity) || 0);
                }, 0);

                var columnWidths = [15, 42, 30, 80, 35, 65, 35, 35, 65, 40, 30, 45, 30];
                var totalColumnWidth = columnWidths.reduce(function (sum, width) { return sum + width; }, 0);

                var columnStyles = {
                    0: { cellWidth: columnWidths[0], halign: 'center' },
                    1: { cellWidth: columnWidths[1], halign: 'center' },
                    2: { cellWidth: columnWidths[2], halign: 'center' },
                    3: { cellWidth: columnWidths[3], halign: 'left' },
                    4: { cellWidth: columnWidths[4], halign: 'center' },
                    5: { cellWidth: columnWidths[5], halign: 'left' },
                    6: { cellWidth: columnWidths[6], halign: 'center' },
                    7: { cellWidth: columnWidths[7], halign: 'center' },
                    8: { cellWidth: columnWidths[8], halign: 'center' },
                    9: { cellWidth: columnWidths[9], halign: 'right' },
                    10: { cellWidth: columnWidths[10], halign: 'center' },
                    11: { cellWidth: columnWidths[11], halign: 'right' },
                    12: { cellWidth: columnWidths[12], halign: 'left' }
                };

                var footRow = [
                    { content: 'Total', colSpan: 9, styles: { halign: 'right', fontStyle: 'bold', fontSize: 8 } },
                    { content: formatAmount(totalSalary), styles: { halign: 'right', fontStyle: 'bold', fontSize: 8 } },
                    { content: '', styles: { halign: 'center', fontStyle: 'bold', fontSize: 8 } },
                    { content: formatAmount(totalGratuity), styles: { halign: 'right', fontStyle: 'bold', fontSize: 8 } },
                    { content: '', styles: {} }
                ];

                doc.autoTable({
                    theme: 'grid',
                    head: head,
                    body: body,
                    foot: [footRow],
                    styles: {
                        font: 'times',
                        fontSize: 7,
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        cellPadding: 2,
                        valign: 'middle',
                        overflow: 'linebreak',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    headStyles: {
                        fillColor: [54, 96, 146],
                        textColor: [255, 255, 255],
                        fontStyle: 'bold',
                        font: 'times',
                        fontSize: 7.5,
                        halign: 'center',
                        valign: 'middle',
                        cellPadding: 2,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0],
                        overflow: 'hidden'
                    },
                    bodyStyles: {
                        fillColor: [255, 255, 255],
                        fontSize: 6.5,
                        overflow: 'linebreak'
                    },
                    alternateRowStyles: {
                        fillColor: [255, 255, 255]
                    },
                    footStyles: {
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        fontStyle: 'bold',
                        fontSize: 7.5,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    columnStyles: columnStyles,
                    tableWidth: totalColumnWidth,
                    margin: {
                        top: topMargin,
                        bottom: bottomMargin,
                        left: leftMargin,
                        right: rightMargin
                    },
                    didParseCell: function (data) {
                        if (data.section === 'body' || data.section === 'foot') {
                            data.cell.styles.fillColor = [255, 255, 255];
                        }
                    },
                    didDrawPage: function () {
                        drawHeader();
                        drawFooter();
                    }
                });

                if (typeof callback === 'function') {
                    callback(doc);
                }
            });
        }

        function exportExcelYearlyBonus(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/ExportToExcelYearlyBonus',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                xhrFields: { responseType: 'blob' },
                success: function (data) {
                    hideLoading();
                    if (!data || data.size === 0) {
                        alert('No data found to download');
                        return;
                    }
                    var url = window.URL.createObjectURL(data);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'PayrollMasterFile_YearlyBonus.xlsx';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                    window.URL.revokeObjectURL(url);
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function buildPdfDocYearlyBonus(filter, rows, callback) {
            getImageBase64FromUrl('/images/DP_logo.png', function (base64Logo, natW, natH) {
                var LOGO_TARGET_HEIGHT = 26;
                var logoWidth = (natH > 0) ? (natW / natH) * LOGO_TARGET_HEIGHT : LOGO_TARGET_HEIGHT;
                var jsPDFCtor = window.jspdf ? window.jspdf.jsPDF : window.jsPDF;
                var CUSTOM_PAGE_WIDTH = 520;
                var A4_PAGE_HEIGHT = 841.89;
                var doc = new jsPDFCtor({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: [CUSTOM_PAGE_WIDTH, A4_PAGE_HEIGHT]
                });

                var pageWidth = doc.internal.pageSize.getWidth();
                var pageHeight = doc.internal.pageSize.getHeight();
                var leftMargin = 15;
                var rightMargin = 15;
                var topMargin = 70;
                var bottomMargin = 40;
                var availableWidth = pageWidth - leftMargin - rightMargin;

                function drawHeader() {
                    if (base64Logo) {
                        doc.addImage(base64Logo, 'PNG', leftMargin, 15, logoWidth, LOGO_TARGET_HEIGHT);
                    }
                    doc.setFont("times", "bold");
                    doc.setFontSize(13);
                    doc.text('DataPath Ltd.', pageWidth / 2, 22, { align: 'center' });
                    doc.setFontSize(11);
                    doc.setFont("times", "normal");
                    doc.text('Yearly Bonus Report', pageWidth / 2, 38, { align: 'center' });
                    doc.setFontSize(8);
                    var periodText = filter.GenerateType === 'ByMonth'
                        ? 'For the month of ' + filter.MonthName + ', ' + filter.YearName
                        : 'For the period ' + (formatDateDMY(filter.DateFrom) || '') + ' to ' + (formatDateDMY(filter.DateTo) || '');
                    doc.text(periodText, pageWidth / 2, 52, { align: 'center' });
                }

                function drawFooter() {
                    var pageCount = doc.internal.getNumberOfPages();
                    var currentPage = doc.internal.getCurrentPageInfo().pageNumber;
                    var now = new Date();
                    var hours = now.getHours();
                    var minutes = now.getMinutes().toString().padStart(2, '0');
                    var ampm = hours >= 12 ? 'PM' : 'AM';
                    hours = hours % 12 || 12;
                    var day = now.getDate().toString().padStart(2, '0');
                    var month = (now.getMonth() + 1).toString().padStart(2, '0');
                    var year = now.getFullYear();
                    var printDateTime = day + '/' + month + '/' + year + ' ' + hours + ':' + minutes + ' ' + ampm;
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    doc.setTextColor(80);
                    doc.text('Print Datetime: ' + printDateTime, leftMargin, pageHeight - 18);
                    doc.text('GCTL Infosys - HRM & Finance System', pageWidth / 2, pageHeight - 18, { align: 'center' });
                    doc.text('Page ' + currentPage + ' of ' + pageCount, pageWidth - rightMargin, pageHeight - 18, { align: 'right' });
                    doc.setTextColor(0);
                }

                var head = [[
                    'SL', 'Emp ID', 'Pay ID', 'Name of the Employee', 'Status', 'Department',
                    'DOH', 'DOT', 'Bank Account No', 'Salary', 'Yearly Bonus', ' Note'
                ]];

                var body = rows.map(function (row) {
                    return [
                        row.sl, row.idNo, row.payId, row.nameOfTheEmployee, row.status,
                        row.department, row.dateOfHire, row.dot, row.bankAccountNo,
                        formatAmount(row.salary), formatAmount(row.yearlyBonus), row.note
                    ];
                });

                var totalSalary = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.salary) || 0);
                }, 0);

                var totalBonus = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.yearlyBonus) || 0);
                }, 0);

                var columnWidths = [15, 42, 25, 80, 28, 60, 35, 35, 62, 35, 44, 30];
                var totalColumnWidth = columnWidths.reduce(function (sum, width) { return sum + width; }, 0);

                var columnStyles = {
                    0: { cellWidth: columnWidths[0], halign: 'center' },
                    1: { cellWidth: columnWidths[1], halign: 'center' },
                    2: { cellWidth: columnWidths[2], halign: 'center' },
                    3: { cellWidth: columnWidths[3], halign: 'left' },
                    4: { cellWidth: columnWidths[4], halign: 'center' },
                    5: { cellWidth: columnWidths[5], halign: 'left' },
                    6: { cellWidth: columnWidths[6], halign: 'center' },
                    7: { cellWidth: columnWidths[7], halign: 'center' },
                    8: { cellWidth: columnWidths[8], halign: 'center' },
                    9: { cellWidth: columnWidths[9], halign: 'right' },
                    10: { cellWidth: columnWidths[10], halign: 'right' },
                    11: { cellWidth: columnWidths[11], halign: 'left' }
                };

                var footRow = [
                    { content: 'Total', colSpan: 9, styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: formatAmount(totalSalary), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: formatAmount(totalBonus), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: '', styles: {} }
                ];

                doc.autoTable({
                    theme: 'grid',
                    head: head,
                    body: body,
                    foot: [footRow],
                    styles: {
                        font: 'times',
                        fontSize: 7,
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        cellPadding: 2,
                        valign: 'middle',
                        overflow: 'linebreak',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    headStyles: {
                        fillColor: [54, 96, 146],
                        textColor: [255, 255, 255],
                        fontStyle: 'bold',
                        font: 'times',
                        fontSize: 6.5,
                        halign: 'center',
                        valign: 'middle',
                        cellPadding: 2,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0],
                        overflow: 'hidden'
                    },
                    bodyStyles: {
                        fillColor: [255, 255, 255],
                        fontSize: 6.5,
                        overflow: 'linebreak'
                    },
                    alternateRowStyles: {
                        fillColor: [255, 255, 255]
                    },
                    footStyles: {
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        fontStyle: 'bold',
                        fontSize: 6.5,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    columnStyles: columnStyles,
                    tableWidth: totalColumnWidth,
                    margin: {
                        top: topMargin,
                        bottom: bottomMargin,
                        left: leftMargin,
                        right: rightMargin
                    },
                    didParseCell: function (data) {
                        if (data.section === 'body' || data.section === 'foot') {
                            data.cell.styles.fillColor = [255, 255, 255];
                        }
                    },
                    didDrawPage: function () {
                        drawHeader();
                        drawFooter();
                    }
                });

                if (typeof callback === 'function') {
                    callback(doc);
                }
            });
        }

        function exportPdfYearlyBonus(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileYearlyBonus',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to download');
                        return;
                    }
                    buildPdfDocYearlyBonus(filter, rows, function (doc) {
                        doc.save('YearlyBonus_Report.pdf');
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function previewPdfYearlyBonus(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileYearlyBonus',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to preview');
                        return;
                    }
                    buildPdfDocYearlyBonus(filter, rows, function (doc) {
                        var blobUrl = doc.output('bloburl');
                        var $container = $('#pdf-preview-container');
                        $container.empty();
                        var $iframe = $('<iframe>', {
                            src: blobUrl,
                            style: 'width:100%; height:100%; border:0;'
                        });
                        $container.append($iframe).show();
                        $container[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function exportExcelExtraDay(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/ExportToExcelExtraDay',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                xhrFields: { responseType: 'blob' },
                success: function (data) {
                    hideLoading();
                    if (!data || data.size === 0) {
                        alert('No data found to download');
                        return;
                    }
                    var url = window.URL.createObjectURL(data);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'PayrollMasterFile_ExtraDay.xlsx';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                    window.URL.revokeObjectURL(url);
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function buildPdfDocExtraDay(filter, rows, callback) {
            
            getImageBase64FromUrl('/images/DP_logo.png', function (base64Logo, natW, natH) {
                var LOGO_TARGET_HEIGHT = 26;
                var logoWidth = (natH > 0) ? (natW / natH) * LOGO_TARGET_HEIGHT : LOGO_TARGET_HEIGHT;
                var jsPDFCtor = window.jspdf ? window.jspdf.jsPDF : window.jsPDF;
                var CUSTOM_PAGE_WIDTH = 560;
                var CUSTOM_PAGE_HEIGHT = 841.89;
                var doc = new jsPDFCtor({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: [CUSTOM_PAGE_WIDTH, CUSTOM_PAGE_HEIGHT]
                });

                var pageWidth = doc.internal.pageSize.getWidth();
                var pageHeight = doc.internal.pageSize.getHeight();
                var leftMargin = 15;
                var rightMargin = 15;
                var topMargin = 70;
                var bottomMargin = 40;
                var availableWidth = pageWidth - leftMargin - rightMargin;

                function drawHeader() {
                    if (base64Logo) {
                        doc.addImage(base64Logo, 'PNG', 18, 18, logoWidth, LOGO_TARGET_HEIGHT);
                    }
                    doc.setFont("times", "bold");
                    doc.setFontSize(13);
                    doc.text('DataPath Ltd.', pageWidth / 2, 22, { align: 'center' });
                    doc.setFontSize(11);
                    doc.setFont("times", "normal");
                    doc.text('Extra Day Report', pageWidth / 2, 38, { align: 'center' });
                    doc.setFontSize(8);
                    var periodText = filter.GenerateType === 'ByMonth'
                        ? 'For the month of ' + filter.MonthName + ', ' + filter.YearName
                        : 'For the period ' + (formatDateDMY(filter.DateFrom) || '') + ' to ' + (formatDateDMY(filter.DateTo) || '');
                    doc.text(periodText, pageWidth / 2, 52, { align: 'center' });
                }

                function drawFooter() {
                    var pageCount = doc.internal.getNumberOfPages();
                    var currentPage = doc.internal.getCurrentPageInfo().pageNumber;
                    var now = new Date();
                    var hours = now.getHours();
                    var minutes = now.getMinutes().toString().padStart(2, '0');
                    var ampm = hours >= 12 ? 'PM' : 'AM';
                    hours = hours % 12 || 12;
                    var day = now.getDate().toString().padStart(2, '0');
                    var month = (now.getMonth() + 1).toString().padStart(2, '0');
                    var year = now.getFullYear();
                    var printDateTime = day + '/' + month + '/' + year + ' ' + hours + ':' + minutes + ' ' + ampm;
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    doc.setTextColor(80);
                    doc.text('Print Datetime: ' + printDateTime, leftMargin, pageHeight - 18);
                    doc.text('GCTL Infosys - HRM & Finance System', pageWidth / 2, pageHeight - 18, { align: 'center' });
                    doc.text('Page ' + currentPage + ' of ' + pageCount, pageWidth - rightMargin, pageHeight - 18, { align: 'right' });
                    doc.setTextColor(0);
                }

                var headers = [
                    'SL', 'ID NO.', 'Pay ID', 'Name of the Employee', 'Status', 'Department',
                    'DOH', 'DOT', 'Bank Account No', 'Salary', 'Days', 'Extra Days Amount', 'Note'
                ];
                var body = rows.map(function (row) {
                    return [
                        row.sl, row.idNo, row.payId, row.nameOfTheEmployee, row.status,
                        row.department, row.dateOfHire, row.dot, row.bankAccountNo,
                        formatAmount(row.salary), formatAmount(row.days),
                        formatAmount(row.extraDaysAmount), row.note
                    ];
                });

                var totalSalary = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.salary) || 0);
                }, 0);

                var totalDays = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.days) || 0);
                }, 0);

                var totalAmount = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.extraDaysAmount) || 0);
                }, 0);

                var columnWidths = [15, 42, 30, 80, 35, 65, 35, 35, 60, 45, 20, 35, 35];
                var totalColumnWidth = columnWidths.reduce(function (sum, width) { return sum + width; }, 0);

                var columnStyles = {};
                for (var c = 0; c < headers.length; c++) {
                    var alignment = 'left';
                    if (c === 0 || c === 1 || c === 2 || c === 4 || c === 6 || c === 7 || c === 8 || c === 10) {
                        alignment = 'center';
                    }
                    if (c === 9 || c === 11) {
                        alignment = 'right';
                    }
                    columnStyles[c] = {
                        cellWidth: columnWidths[c],
                        halign: alignment,
                        valign: 'middle',
                        overflow: 'linebreak'
                    };
                }

                var footRow = [
                    { content: 'Total', colSpan: 9, styles: { halign: 'right', valign: 'middle', fontStyle: 'bold', fontSize: 7 } },
                    { content: formatAmount(totalSalary), styles: { halign: 'right', valign: 'middle', fontStyle: 'bold', fontSize: 7 } },
                    { content: formatAmount(totalDays), styles: { halign: 'center', valign: 'middle', fontStyle: 'bold', fontSize: 7 } },
                    { content: formatAmount(totalAmount), styles: { halign: 'right', valign: 'middle', fontStyle: 'bold', fontSize: 7 } },
                    { content: '', styles: { halign: 'left', valign: 'middle', fontStyle: 'bold', fontSize: 7 } }
                ];

                doc.autoTable({
                    theme: 'grid',
                    head: [headers],
                    body: body,
                    foot: [footRow],
                    showFoot: 'lastPage',
                    styles: {
                        font: 'times',
                        fontSize: 6.5,
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        cellPadding: 2,
                        valign: 'middle',
                        overflow: 'linebreak',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    headStyles: {
                        fillColor: [54, 96, 146],
                        textColor: [255, 255, 255],
                        fontStyle: 'bold',
                        font: 'times',
                        fontSize: 6.5,
                        halign: 'center',
                        valign: 'middle',
                        cellPadding: 2,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0],
                        overflow: 'hidden'
                    },
                    bodyStyles: {
                        fillColor: [255, 255, 255],
                        fontSize: 6.5,
                        overflow: 'linebreak'
                    },
                    alternateRowStyles: {
                        fillColor: [255, 255, 255]
                    },
                    footStyles: {
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        fontStyle: 'bold',
                        fontSize: 7,
                        valign: 'middle',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    columnStyles: columnStyles,
                    tableWidth: totalColumnWidth,
                    margin: {
                        top: topMargin,
                        bottom: bottomMargin,
                        left: leftMargin,
                        right: rightMargin
                    },
                    didParseCell: function (data) {
                        if (data.section === 'head') {
                            data.cell.styles.fontSize = 6.5;
                            data.cell.styles.fontStyle = 'bold';
                            data.cell.styles.halign = 'center';
                            data.cell.styles.valign = 'middle';
                            data.cell.styles.overflow = 'hidden';
                        }
                        if (data.section === 'body' || data.section === 'foot') {
                            data.cell.styles.fillColor = [255, 255, 255];
                        }
                        if (data.section === 'foot') {
                            data.cell.styles.fontStyle = 'bold';
                            data.cell.styles.fontSize = 7;
                        }
                    },
                    didDrawPage: function () {
                        drawHeader();
                        drawFooter();
                    }
                });

                if (typeof callback === 'function') {
                    callback(doc);
                }
            });
        }

        function exportPdfExtraDay(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileExtraDay',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to download');
                        return;
                    }
                    buildPdfDocExtraDay(filter, rows, function (doc) {
                        doc.save('ExtraDay_Report.pdf');
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function previewPdfExtraDay(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileExtraDay',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to preview');
                        return;
                    }
                    buildPdfDocExtraDay(filter, rows, function (doc) {
                        var blobUrl = doc.output('bloburl');
                        var $container = $('#pdf-preview-container');
                        $container.empty();
                        var $iframe = $('<iframe>', {
                            src: blobUrl,
                            style: 'width:100%; height:100%; border:0;'
                        });
                        $container.append($iframe).show();
                        $container[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function exportExcelTerminated(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/ExportToExcelTerminated',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                xhrFields: { responseType: 'blob' },
                success: function (data) {
                    hideLoading();
                    if (!data || data.size === 0) {
                        alert('No data found to download');
                        return;
                    }
                    var url = window.URL.createObjectURL(data);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'PayrollMasterFile_Terminated.xlsx';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                    window.URL.revokeObjectURL(url);
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function buildPdfDocTerminated(filter, rows, callback) {
            getImageBase64FromUrl('/images/DP_logo.png', function (base64Logo, natW, natH) {
                var LOGO_TARGET_HEIGHT = 26;
                var logoWidth = (natH > 0) ? (natW / natH) * LOGO_TARGET_HEIGHT : LOGO_TARGET_HEIGHT;
                var jsPDFCtor = window.jspdf ? window.jspdf.jsPDF : window.jsPDF;
                var CUSTOM_PAGE_WIDTH = 630;
                var CUSTOM_PAGE_HEIGHT = 841.89;
                var doc = new jsPDFCtor({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: [CUSTOM_PAGE_WIDTH, CUSTOM_PAGE_HEIGHT]
                });

                var pageWidth = doc.internal.pageSize.getWidth();
                var pageHeight = doc.internal.pageSize.getHeight();
                var leftMargin = 15;
                var rightMargin = 15;
                var topMargin = 70;
                var bottomMargin = 40;

                function drawHeader() {
                    if (base64Logo) {
                        doc.addImage(base64Logo, 'PNG', 18, 18, logoWidth, LOGO_TARGET_HEIGHT);
                    }
                    doc.setFont("times", "bold");
                    doc.setFontSize(13);
                    doc.text('DataPath Ltd.', pageWidth / 2, 22, { align: 'center' });
                    doc.setFontSize(11);
                    doc.setFont("times", "normal");
                    doc.text('Terminated Report', pageWidth / 2, 38, { align: 'center' });
                    doc.setFontSize(8);
                    var periodText = filter.GenerateType === 'ByMonth'
                        ? 'For the month of ' + filter.MonthName + ', ' + filter.YearName
                        : 'For the period ' + (formatDateDMY(filter.DateFrom) || '') + ' to ' + (formatDateDMY(filter.DateTo) || '');
                    doc.text(periodText, pageWidth / 2, 52, { align: 'center' });
                }

                function drawFooter() {
                    var pageCount = doc.internal.getNumberOfPages();
                    var currentPage = doc.internal.getCurrentPageInfo().pageNumber;
                    var now = new Date();
                    var hours = now.getHours();
                    var minutes = now.getMinutes().toString().padStart(2, '0');
                    var ampm = hours >= 12 ? 'PM' : 'AM';
                    hours = hours % 12 || 12;
                    var day = now.getDate().toString().padStart(2, '0');
                    var month = (now.getMonth() + 1).toString().padStart(2, '0');
                    var year = now.getFullYear();
                    var printDateTime = day + '/' + month + '/' + year + ' ' + hours + ':' + minutes + ' ' + ampm;
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    doc.setTextColor(80);
                    doc.text('Print Datetime: ' + printDateTime, leftMargin, pageHeight - 18);
                    doc.text('GCTL Infosys - HRM & Finance System', pageWidth / 2, pageHeight - 18, { align: 'center' });
                    doc.text('Page ' + currentPage + ' of ' + pageCount, pageWidth - rightMargin, pageHeight - 18, { align: 'right' });
                    doc.setTextColor(0);
                }

                var headers = [
                    'SL', 'ID NO.', 'Pay ID', 'Name of the Employee', 'Department', 'Status',
                    'DOH', 'DOT', 'DBBL', 'UCBL', 'Duration', 'PF Eligibility', 'Days', 'Note'
                ];

                var body = rows.map(function (row) {
                    return [
                        row.sl, row.idNo, row.payId, row.nameOfTheEmployee, row.department,
                        row.status, row.dateOfHire, row.dot, row.dbbl, row.ucbl, row.duration,
                        row.pfEligiblity != null ? parseFloat(row.pfEligiblity).toFixed(0) : '',
                        row.days != null ? parseInt(row.days) : '',
                        row.note
                    ];
                });

                var totalPf = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.pfEligiblity) || 0);
                }, 0);

                var totalDays = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.days) || 0);
                }, 0);
                
                var columnWidths = [15, 42, 25, 82, 60, 35, 35, 35, 55, 55, 75, 35, 20, 35];
                var totalColumnWidth = columnWidths.reduce(function (sum, width) { return sum + width; }, 0);

                var columnStyles = {};
                for (var c = 0; c < headers.length; c++) {
                    var alignment = 'left';
                    if (c === 0 || c === 1 || c === 2 || c === 5 || c === 6 || c === 7 || c === 8 || c === 9 || c === 10 || c === 11 || c === 12) {
                        alignment = 'center';
                    }
                    columnStyles[c] = {
                        cellWidth: columnWidths[c],
                        halign: alignment,
                        valign: 'middle',
                        overflow: 'linebreak'
                    };
                }

                var totalRow = new Array(headers.length).fill('');
                totalRow[11] = totalPf.toFixed(0);
                totalRow[12] = totalDays.toString();
                body.push(totalRow);

                doc.autoTable({
                    theme: 'grid',
                    head: [headers],
                    body: body,
                    styles: {
                        font: 'times',
                        fontSize: 6.5,
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        cellPadding: 2,
                        valign: 'middle',
                        overflow: 'linebreak',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    headStyles: {
                        fillColor: [54, 96, 146],
                        textColor: [255, 255, 255],
                        fontStyle: 'bold',
                        font: 'times',
                        fontSize: 6.5,
                        halign: 'center',
                        valign: 'middle',
                        cellPadding: 2,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0],
                        overflow: 'hidden'
                    },
                    bodyStyles: {
                        fillColor: [255, 255, 255],
                        fontSize: 6.5,
                        overflow: 'linebreak'
                    },
                    alternateRowStyles: {
                        fillColor: [255, 255, 255]
                    },
                    columnStyles: columnStyles,
                    tableWidth: totalColumnWidth,
                    margin: {
                        top: topMargin,
                        bottom: bottomMargin,
                        left: leftMargin,
                        right: rightMargin
                    },
                    didParseCell: function (data) {
                        if (data.section === 'head') {
                            data.cell.styles.fontSize = 6.5;
                            data.cell.styles.fontStyle = 'bold';
                            data.cell.styles.halign = 'center';
                            data.cell.styles.valign = 'middle';
                            data.cell.styles.overflow = 'hidden';
                        }
                        if (data.section === 'body' && data.row.index === body.length - 1) {
                            data.cell.styles.fontStyle = 'bold';
                            data.cell.styles.fontSize = 6.5;
                            data.cell.styles.fillColor = [255, 255, 255];
                            if (data.column.index >= 0 && data.column.index <= 10) {
                                if (data.column.index === 0) {
                                    data.cell.text = ['Total'];
                                    data.cell.styles.halign = 'right';
                                    data.cell.colSpan = 11;
                                } else {
                                    data.cell.text = [];
                                }
                            }
                            if (data.column.index === 11) {
                                data.cell.text = [totalPf.toFixed(0)];
                                data.cell.styles.halign = 'center';
                            }
                            if (data.column.index === 12) {
                                data.cell.text = [totalDays.toString()];
                                data.cell.styles.halign = 'center';
                            }
                            if (data.column.index === 13) {
                                data.cell.text = [];
                            }
                        }
                        if (data.section === 'body') {
                            data.cell.styles.fillColor = [255, 255, 255];
                        }
                    },
                    didDrawCell: function (data) {
                        if (data.section === 'body' && data.row.index === body.length - 1) {
                            data.cell.styles.fillColor = [255, 255, 255];
                        }
                    },
                    didDrawPage: function () {
                        drawHeader();
                        drawFooter();
                    }
                });

                if (typeof callback === 'function') {
                    callback(doc);
                }
            });
        }

        function exportPdfTerminated(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileTerminated',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to download');
                        return;
                    }
                    buildPdfDocTerminated(filter, rows, function (doc) {
                        doc.save('Terminated_Report.pdf');
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function previewPdfTerminated(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileTerminated',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to preview');
                        return;
                    }
                    buildPdfDocTerminated(filter, rows, function (doc) {
                        var blobUrl = doc.output('bloburl');
                        var $container = $('#pdf-preview-container');
                        $container.empty();
                        var $iframe = $('<iframe>', {
                            src: blobUrl,
                            style: 'width:100%; height:100%; border:0;'
                        });
                        $container.append($iframe).show();
                        $container[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function exportExcelIncentives(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/ExportToExcelIncentives',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                xhrFields: { responseType: 'blob' },
                success: function (data) {
                    hideLoading();
                    if (!data || data.size === 0) {
                        alert('No data found to download');
                        return;
                    }
                    var url = window.URL.createObjectURL(data);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'PayrollMasterFile_Incentives.xlsx';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                    window.URL.revokeObjectURL(url);
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function buildPdfDocIncentives(filter, rows, callback) {
            getImageBase64FromUrl('/images/DP_logo.png', function (base64Logo, natW, natH) {
                var LOGO_TARGET_HEIGHT = 26;
                var logoWidth = (natH > 0) ? (natW / natH) * LOGO_TARGET_HEIGHT : LOGO_TARGET_HEIGHT;
                var jsPDFCtor = window.jspdf ? window.jspdf.jsPDF : window.jsPDF;
                var CUSTOM_PAGE_WIDTH = 690;
                var A4_PAGE_HEIGHT = 841.89;
                var doc = new jsPDFCtor({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: [CUSTOM_PAGE_WIDTH, A4_PAGE_HEIGHT]
                });

                var pageWidth = doc.internal.pageSize.getWidth();
                var pageHeight = doc.internal.pageSize.getHeight();
                var leftMargin = 15;
                var rightMargin = 15;
                var topMargin = 70;
                var bottomMargin = 40;

                function drawHeader() {
                    if (base64Logo) {
                        doc.addImage(base64Logo, 'PNG', 15, 15, logoWidth, LOGO_TARGET_HEIGHT);
                    }
                    doc.setFont("times", "bold");
                    doc.setFontSize(13);
                    doc.text('DataPath Ltd.', pageWidth / 2, 22, { align: 'center' });
                    doc.setFont("times", "normal");
                    doc.setFontSize(11);
                    doc.text('Incentives Report', pageWidth / 2, 38, { align: 'center' });
                    doc.setFontSize(8);
                    var periodText = filter.GenerateType === 'ByMonth'
                        ? 'For the month of ' + filter.MonthName + ', ' + filter.YearName
                        : 'For the period ' + (formatDateDMY(filter.DateFrom) || '') + ' to ' + (formatDateDMY(filter.DateTo) || '');
                    doc.text(periodText, pageWidth / 2, 52, { align: 'center' });
                }

                function drawFooter() {
                    var pageCount = doc.internal.getNumberOfPages();
                    var currentPage = doc.internal.getCurrentPageInfo().pageNumber;
                    var now = new Date();
                    var hours = now.getHours();
                    var minutes = now.getMinutes().toString().padStart(2, '0');
                    var ampm = hours >= 12 ? 'PM' : 'AM';
                    hours = hours % 12 || 12;
                    var day = now.getDate().toString().padStart(2, '0');
                    var month = (now.getMonth() + 1).toString().padStart(2, '0');
                    var year = now.getFullYear();
                    var printDateTime = day + '/' + month + '/' + year + ' ' + hours + ':' + minutes + ' ' + ampm;
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    doc.setTextColor(80);
                    doc.text('Print Datetime: ' + printDateTime, leftMargin, pageHeight - 18);
                    doc.text('GCTL Infosys - HRM & Finance System', pageWidth / 2, pageHeight - 18, { align: 'center' });
                    doc.text('Page ' + currentPage + ' of ' + pageCount, pageWidth - rightMargin, pageHeight - 18, { align: 'right' });
                    doc.setTextColor(0);
                }

                var headers = [
                    'SL', 'ID No.', 'Pay ID', 'Name of the Employee', 'Status', 'Department',
                    'Date of Hire', 'DOT', 'DBBL', 'Bank Account No', 'Salary', 'Designation',
                    'Incentives', 'Eligibility', 'Notes'
                ];

                var body = rows.map(function (row) {
                    return [
                        row.sl, row.idNo, row.payId, row.nameOfTheEmployee, row.status,
                        row.department, row.dateOfHire, row.dot, row.dbbl, row.bankAccountNo,
                        row.salary != null ? parseFloat(row.salary).toLocaleString('en-US') : '',
                        row.designation,
                        row.incentives != null ? parseFloat(row.incentives).toLocaleString('en-US') : '',
                        row.eligibility, row.notes
                    ];
                });

                var totalSalary = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.salary) || 0);
                }, 0);

                var totalIncentives = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.incentives) || 0);
                }, 0);

                var totalRow = new Array(headers.length).fill('');
                body.push(totalRow);

                var columnWidths = [15, 42, 30, 80, 28, 55, 45, 45, 60, 55, 40, 55, 40, 35, 30];
                var totalColumnWidth = columnWidths.reduce(function (sum, width) { return sum + width; }, 0);

                var columnStyles = {};
                for (var c = 0; c < headers.length; c++) {
                    var alignment = 'left';
                    if (c === 0 || c === 1 || c === 2 || c === 4 || c === 6 || c === 7 || c === 8 || c === 9 || c === 13) {
                        alignment = 'center';
                    }
                    if (c === 10 || c === 12) {
                        alignment = 'right';
                    }
                    columnStyles[c] = {
                        cellWidth: columnWidths[c],
                        halign: alignment,
                        valign: 'middle',
                        overflow: 'linebreak'
                    };
                }

                doc.autoTable({
                    theme: 'grid',
                    head: [headers],
                    body: body,
                    styles: {
                        font: 'times',
                        fontSize: 7,
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        cellPadding: 2,
                        valign: 'middle',
                        overflow: 'linebreak',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    headStyles: {
                        fillColor: [54, 96, 146],
                        textColor: [255, 255, 255],
                        fontStyle: 'bold',
                        font: 'times',
                        fontSize: 7.5,
                        halign: 'center',
                        valign: 'middle',
                        cellPadding: 2,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0],
                        overflow: 'hidden'
                    },
                    bodyStyles: {
                        fillColor: [255, 255, 255],
                        fontSize: 6.5,
                        overflow: 'linebreak'
                    },
                    alternateRowStyles: {
                        fillColor: [255, 255, 255]
                    },
                    columnStyles: columnStyles,
                    tableWidth: totalColumnWidth,
                    margin: {
                        top: topMargin,
                        bottom: bottomMargin,
                        left: leftMargin,
                        right: rightMargin
                    },
                    didParseCell: function (data) {
                        if (data.section === 'head') {
                            data.cell.styles.fontSize = 7.5;
                            data.cell.styles.fontStyle = 'bold';
                            data.cell.styles.halign = 'center';
                            data.cell.styles.valign = 'middle';
                            data.cell.styles.overflow = 'hidden';
                        }
                        if (data.section === 'body' && data.row.index === body.length - 1) {
                            data.cell.styles.fontStyle = 'bold';
                            data.cell.styles.fontSize = 7.5;
                            data.cell.styles.fillColor = [255, 255, 255];
                            if (data.column.index === 0) {
                                data.cell.colSpan = 10;
                                data.cell.text = ['Total'];
                                data.cell.styles.halign = 'right';
                                data.cell.styles.valign = 'middle';
                                data.cell.styles.fontStyle = 'bold';
                                data.cell.styles.fontSize = 7.5;
                                data.cell.styles.cellPadding = 2;
                            }
                            if (data.column.index >= 1 && data.column.index <= 9) {
                                data.cell.text = [];
                            }
                            if (data.column.index === 10) {
                                data.cell.text = [formatAmount(totalSalary)];
                                data.cell.styles.halign = 'right';
                                data.cell.styles.fontStyle = 'bold';
                            }
                            if (data.column.index === 11) {
                                data.cell.text = [];
                            }
                            if (data.column.index === 12) {
                                data.cell.text = [formatAmount(totalIncentives)];
                                data.cell.styles.halign = 'right';
                                data.cell.styles.fontStyle = 'bold';
                            }
                            if (data.column.index === 13 || data.column.index === 14) {
                                data.cell.text = [];
                            }
                        }
                        if (data.section === 'body') {
                            data.cell.styles.fillColor = [255, 255, 255];
                        }
                    },
                    didDrawCell: function (data) {
                        if (data.section === 'body' && data.row.index === body.length - 1) {
                            data.cell.styles.fillColor = [255, 255, 255];
                        }
                    },
                    didDrawPage: function () {
                        drawHeader();
                        drawFooter();
                    }
                });

                if (typeof callback === 'function') {
                    callback(doc);
                }
            });
        }

        function exportPdfIncentives(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileIncentives',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to download');
                        return;
                    }
                    buildPdfDocIncentives(filter, rows, function (doc) {
                        doc.save('Incentives_Report.pdf');
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function previewPdfIncentives(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileIncentives',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to preview');
                        return;
                    }
                    buildPdfDocIncentives(filter, rows, function (doc) {
                        var blobUrl = doc.output('bloburl');
                        var $container = $('#pdf-preview-container');
                        $container.empty();
                        var $iframe = $('<iframe>', {
                            src: blobUrl,
                            style: 'width:100%; height:100%; border:0;'
                        });
                        $container.append($iframe).show();
                        $container[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        // New Hire Export Excel Function
        function exportExcelNewHire(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/ExportToExcelNewHire',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                xhrFields: { responseType: 'blob' },
                success: function (data) {
                    hideLoading();
                    if (!data || data.size === 0) {
                        alert('No data found to download');
                        return;
                    }
                    var url = window.URL.createObjectURL(data);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'PayrollMasterFile_NewHire.xlsx';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                    window.URL.revokeObjectURL(url);
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        // New Hire Build PDF Function
        function buildPdfDocNewHire(filter, rows, callback) {
            getImageBase64FromUrl('/images/DP_logo.png', function (base64Logo, natW, natH) {
                var LOGO_TARGET_HEIGHT = 26;
                var logoWidth = (natH > 0) ? (natW / natH) * LOGO_TARGET_HEIGHT : LOGO_TARGET_HEIGHT;
                var jsPDFCtor = window.jspdf ? window.jspdf.jsPDF : window.jsPDF;
                var CUSTOM_PAGE_WIDTH = 645;
                var A4_PAGE_HEIGHT = 841.89;
                var doc = new jsPDFCtor({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: [CUSTOM_PAGE_WIDTH, A4_PAGE_HEIGHT]
                });

                var pageWidth = doc.internal.pageSize.getWidth();
                var pageHeight = doc.internal.pageSize.getHeight();
                var leftMargin = 15;
                var rightMargin = 15;
                var topMargin = 70;
                var bottomMargin = 40;

                function drawHeader() {
                    if (base64Logo) {
                        doc.addImage(base64Logo, 'PNG', 15, 15, logoWidth, LOGO_TARGET_HEIGHT);
                    }
                    doc.setFont("times", "bold");
                    doc.setFontSize(13);
                    doc.text('DataPath Ltd.', pageWidth / 2, 22, { align: 'center' });
                    doc.setFont("times", "normal");
                    doc.setFontSize(11);
                    doc.text('New Hire Report', pageWidth / 2, 38, { align: 'center' });
                    doc.setFontSize(8);
                    var periodText = filter.GenerateType === 'ByMonth'
                        ? 'For the month of ' + filter.MonthName + ', ' + filter.YearName
                        : 'For the period ' + (formatDateDMY(filter.DateFrom) || '') + ' to ' + (formatDateDMY(filter.DateTo) || '');
                    doc.text(periodText, pageWidth / 2, 52, { align: 'center' });
                }

                function drawFooter() {
                    var pageCount = doc.internal.getNumberOfPages();
                    var currentPage = doc.internal.getCurrentPageInfo().pageNumber;
                    var now = new Date();
                    var hours = now.getHours();
                    var minutes = now.getMinutes().toString().padStart(2, '0');
                    var ampm = hours >= 12 ? 'PM' : 'AM';
                    hours = hours % 12 || 12;
                    var day = now.getDate().toString().padStart(2, '0');
                    var month = (now.getMonth() + 1).toString().padStart(2, '0');
                    var year = now.getFullYear();
                    var printDateTime = day + '/' + month + '/' + year + ' ' + hours + ':' + minutes + ' ' + ampm;
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    doc.setTextColor(80);
                    doc.text('Print Datetime: ' + printDateTime, leftMargin, pageHeight - 18);
                    doc.text('GCTL Infosys - HRM & Finance System', pageWidth / 2, pageHeight - 18, { align: 'center' });
                    doc.text('Page ' + currentPage + ' of ' + pageCount, pageWidth - rightMargin, pageHeight - 18, { align: 'right' });
                    doc.setTextColor(0);
                }

                var headers = [
                    'SL', 'ID NO.', 'Pay ID', 'Name of the Employee', 'Status', 'Department',
                    'DOH', 'DOT', 'DBBL', 'Bank Account No', 'Gross Salary', 'Days Worked', 'Payment', 'Note'
                ];

                var body = rows.map(function (row) {
                    return [
                        row.sl, row.idNo, row.payId, row.nameOfTheEmployee, row.status,
                        row.department, row.dateOfHire, row.dot, row.bankAcNo, row.bankAcNoUCBL,
                        row.grossSalary != null ? parseFloat(row.grossSalary).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '',
                        row.daysWorked,
                        row.payment != null ? parseFloat(row.payment).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '',
                        row.note
                    ];
                });

                var totalGrossSalary = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.grossSalary) || 0);
                }, 0);

                var totalPayment = rows.reduce(function (sum, row) {
                    return sum + (parseFloat(row.payment) || 0);
                }, 0);

                var columnWidths = [15, 42, 28, 75, 35, 55, 35, 35, 55, 55, 50, 40, 50, 45];
                var totalColumnWidth = columnWidths.reduce(function (sum, width) { return sum + width; }, 0);

                var columnStyles = {};
                for (var c = 0; c < headers.length; c++) {
                    var alignment = 'left';
                    if (c === 0 || c === 1 || c === 2 || c === 4 || c === 6 || c === 7 || c === 8 || c === 9 || c === 11 || c === 13) {
                        alignment = 'center';
                    }
                    if (c === 10 || c === 12) {
                        alignment = 'right';
                    }
                    columnStyles[c] = {
                        cellWidth: columnWidths[c],
                        halign: alignment,
                        valign: 'middle',
                        overflow: 'linebreak'
                    };
                }

                var footRow = [
                    { content: 'Total', colSpan: 10, styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: totalGrossSalary.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: '', styles: {} },
                    { content: totalPayment.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: '', styles: {} }
                ];

                doc.autoTable({
                    theme: 'grid',
                    head: [headers],
                    body: body,
                    foot: [footRow],
                    showFoot: 'lastPage',
                    styles: {
                        font: 'times',
                        fontSize: 6,
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        cellPadding: 2,
                        valign: 'middle',
                        overflow: 'linebreak',
                        lineWidth: 0.1,
                        lineColor: [0, 0, 0]
                    },
                    headStyles: {
                        fillColor: [54, 96, 146],
                        textColor: [255, 255, 255],
                        fontStyle: 'bold',
                        font: 'times',
                        fontSize: 6,
                        halign: 'center',
                        valign: 'middle',
                        lineWidth: 0.1,
                        lineColor: [0, 0, 0]
                    },
                    alternateRowStyles: { fillColor: [255, 255, 255] },
                    bodyStyles: { fillColor: [255, 255, 255] },
                    footStyles: {
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        fontStyle: 'bold',
                        fontSize: 6,
                        lineWidth: 0.1,
                        lineColor: [0, 0, 0]
                    },
                    columnStyles: columnStyles,
                    tableWidth: totalColumnWidth,
                    margin: { top: topMargin, bottom: bottomMargin, left: leftMargin, right: rightMargin },
                    didDrawPage: function () {
                        drawHeader();
                        drawFooter();
                    }
                });

                if (typeof callback === 'function') {
                    callback(doc);
                }
            });
        }

        // New Hire Export PDF Function
        function exportPdfNewHire(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileNewHire',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to download');
                        return;
                    }
                    buildPdfDocNewHire(filter, rows, function (doc) {
                        doc.save('PayrollMasterFile_NewHire.pdf');
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        // New Hire Preview PDF Function
        function previewPdfNewHire(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileNewHire',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to preview');
                        return;
                    }
                    buildPdfDocNewHire(filter, rows, function (doc) {
                        var blobUrl = doc.output('bloburl');
                        var $container = $('#pdf-preview-container');
                        $container.empty();
                        var $iframe = $('<iframe>', {
                            src: blobUrl,
                            style: 'width:100%; height:100%; border:0;'
                        });
                        $container.append($iframe).show();
                        $container[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }
        function exportExcelWfh(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/ExportToExcelWfh',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                xhrFields: { responseType: 'blob' },
                success: function (data) {
                    hideLoading();
                    if (!data || data.size === 0) {
                        alert('No data found to download');
                        return;
                    }
                    var url = window.URL.createObjectURL(data);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'PayrollMasterFile_WFH.xlsx';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                    window.URL.revokeObjectURL(url);
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function buildPdfDocWfh(filter, rows, callback) {
            getImageBase64FromUrl('/images/DP_logo.png', function (base64Logo, natW, natH) {
                var LOGO_TARGET_HEIGHT = 26;
                var logoWidth = (natH > 0) ? (natW / natH) * LOGO_TARGET_HEIGHT : LOGO_TARGET_HEIGHT;
                var jsPDFCtor = window.jspdf ? window.jspdf.jsPDF : window.jsPDF;
                var CUSTOM_PAGE_WIDTH = 700;
                var LEGAL_PAGE_HEIGHT = 1008;

                var doc = new jsPDFCtor({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: [CUSTOM_PAGE_WIDTH, LEGAL_PAGE_HEIGHT]
                });

                var pageWidth = doc.internal.pageSize.getWidth();
                var pageHeight = doc.internal.pageSize.getHeight();
                var leftMargin = 15;
                var rightMargin = 15;
                var topMargin = 70;
                var bottomMargin = 40;

                function drawHeader() {
                    if (base64Logo) {
                        doc.addImage(base64Logo, 'PNG', 15, 15, logoWidth, LOGO_TARGET_HEIGHT);
                    }
                    doc.setFont("times", "bold");
                    doc.setFontSize(13);
                    doc.text('DataPath Ltd.', pageWidth / 2, 22, { align: 'center' });
                    doc.setFont("times", "normal");
                    doc.setFontSize(11);
                    doc.text('WFH Report', pageWidth / 2, 38, { align: 'center' });
                    doc.setFontSize(8);
                    var periodText = filter.GenerateType === 'ByMonth'
                        ? 'For the month of ' + filter.MonthName + ', ' + filter.YearName
                        : 'For the period ' + (formatDateDMY(filter.DateFrom) || '') + ' to ' + (formatDateDMY(filter.DateTo) || '');
                    doc.text(periodText, pageWidth / 2, 52, { align: 'center' });
                }

                function drawFooter() {
                    var pageCount = doc.internal.getNumberOfPages();
                    var currentPage = doc.internal.getCurrentPageInfo().pageNumber;
                    var now = new Date();
                    var hours = now.getHours();
                    var minutes = now.getMinutes().toString().padStart(2, '0');
                    var ampm = hours >= 12 ? 'PM' : 'AM';
                    hours = hours % 12 || 12;
                    var day = now.getDate().toString().padStart(2, '0');
                    var month = (now.getMonth() + 1).toString().padStart(2, '0');
                    var year = now.getFullYear();
                    var printDateTime = day + '/' + month + '/' + year + ' ' + hours + ':' + minutes + ' ' + ampm;
                    doc.setFont("times", "normal");
                    doc.setFontSize(8);
                    doc.setTextColor(80);
                    doc.text('Print Datetime: ' + printDateTime, leftMargin, pageHeight - 18);
                    doc.text('GCTL Infosys - HRM & Finance System', pageWidth / 2, pageHeight - 18, { align: 'center' });
                    doc.text('Page ' + currentPage + ' of ' + pageCount, pageWidth - rightMargin, pageHeight - 18, { align: 'right' });
                    doc.setTextColor(0);
                }

                var headers = [
                    'SL', 'ID NO.', 'Pay ID', 'Name of the Employee', 'Status', 'Department',
                    'DOH', 'DOT', 'DBBL', 'Bank Account No', 'Gross Salary',
                    'Internet', 'Carrying', 'Others', 'Total', 'Note'
                ];

                var body = rows.map(function (row) {
                    return [
                        row.sl, row.idNo, row.payId, row.nameOfTheEmployee, row.status,
                        row.department, row.dateOfHire, row.dot, row.bankAcNo, row.bankAcNoUCBL,
                        row.grossSalary != null ? parseFloat(row.grossSalary).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '',
                        row.internet != null ? parseFloat(row.internet).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '',
                        row.carrying != null ? parseFloat(row.carrying).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '',
                        row.others != null ? parseFloat(row.others).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '',
                        row.total != null ? parseFloat(row.total).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '',
                        row.note
                    ];
                });

                var totalGross = rows.reduce(function (sum, row) { return sum + (parseFloat(row.grossSalary) || 0); }, 0);
                var totalInternet = rows.reduce(function (sum, row) { return sum + (parseFloat(row.internet) || 0); }, 0);
                var totalCarrying = rows.reduce(function (sum, row) { return sum + (parseFloat(row.carrying) || 0); }, 0);
                var totalOthers = rows.reduce(function (sum, row) { return sum + (parseFloat(row.others) || 0); }, 0);
                var grandTotal = rows.reduce(function (sum, row) { return sum + (parseFloat(row.total) || 0); }, 0);

                var columnWidths = [15, 40, 25, 75, 28, 55, 35, 35, 50, 55, 50, 40, 40, 40, 48, 40];
                var totalColumnWidth = columnWidths.reduce(function (sum, width) { return sum + width; }, 0);

                var columnStyles = {};
                for (var c = 0; c < headers.length; c++) {
                    var alignment = 'left';
                    if (c === 0 || c === 1 || c === 2 || c === 4 || c === 6 || c === 7 || c === 8 || c === 9) {
                        alignment = 'center';
                    }
                    if (c >= 10 && c <= 14) {
                        alignment = 'right';
                    }
                    columnStyles[c] = {
                        cellWidth: columnWidths[c],
                        halign: alignment,
                        valign: 'middle',
                        overflow: 'linebreak'
                    };
                }

                var footRow = [
                    { content: 'Total', colSpan: 10, styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: totalGross.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: totalInternet.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: totalCarrying.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: totalOthers.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: grandTotal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }), styles: { halign: 'right', fontStyle: 'bold', fontSize: 7 } },
                    { content: '', styles: {} }
                ];

                doc.autoTable({
                    theme: 'grid',
                    head: [headers],
                    body: body,
                    foot: [footRow],
                    showFoot: 'lastPage',
                    styles: {
                        font: 'times',
                        fontSize: 6.5,
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        cellPadding: 2,
                        valign: 'middle',
                        overflow: 'linebreak',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    headStyles: {
                        fillColor: [54, 96, 146],
                        textColor: [255, 255, 255],
                        fontStyle: 'bold',
                        font: 'times',
                        fontSize: 6.5,
                        halign: 'center',
                        valign: 'middle',
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    alternateRowStyles: { fillColor: [255, 255, 255] },
                    bodyStyles: { fillColor: [255, 255, 255] },
                    footStyles: {
                        fillColor: [255, 255, 255],
                        textColor: [0, 0, 0],
                        fontStyle: 'bold',
                        fontSize: 7,
                        lineWidth: 0.3,
                        lineColor: [0, 0, 0]
                    },
                    columnStyles: columnStyles,
                    tableWidth: totalColumnWidth,
                    margin: { top: topMargin, bottom: bottomMargin, left: leftMargin, right: rightMargin },
                    didDrawPage: function () {
                        drawHeader();
                        drawFooter();
                    }
                });

                if (typeof callback === 'function') {
                    callback(doc);
                }
            });
        }

        function exportPdfWfh(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileWfh',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to download');
                        return;
                    }
                    buildPdfDocWfh(filter, rows, function (doc) {
                        doc.save('WFH_Report.pdf');
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }

        function previewPdfWfh(filter) {
            showLoading();
            $.ajax({
                url: settings.baseUrl + '/GetPayrollMasterFileWfh',
                type: 'POST',
                data: JSON.stringify(filter),
                contentType: 'application/json',
                success: function (res) {
                    hideLoading();
                    if (!res.success) {
                        alert(res.message || 'Failed to load data');
                        return;
                    }
                    var rows = res.data || [];
                    if (rows.length === 0) {
                        alert('No data found to preview');
                        return;
                    }
                    buildPdfDocWfh(filter, rows, function (doc) {
                        var blobUrl = doc.output('bloburl');
                        var $container = $('#pdf-preview-container');
                        $container.empty();
                        var $iframe = $('<iframe>', {
                            src: blobUrl,
                            style: 'width:100%; height:100%; border:0;'
                        });
                        $container.append($iframe).show();
                        $container[0].scrollIntoView({ behavior: 'smooth', block: 'start' });
                    });
                },
                error: function () {
                    hideLoading();
                    alert('Failed to load data');
                }
            });
        }
        $('#btnExportReport').off('click').on('click', function () {
            var filter = buildFilter();
            var format = filter.ExportFormat;
            var masterVal = $("#masterFileTypeSelect").val();

            if (!masterVal) {
                alert("Select Master File Type");
                $("#masterFileTypeSelect").select2('open');
                return;
            }

            if (format === 'Excel') {
                if (masterVal === "01") exportExcel(filter);
                else if (masterVal === "02") exportExcelGratuity(filter);
                else if (masterVal === "03") exportExcelYearlyBonus(filter);
                else if (masterVal === "04") exportExcelExtraDay(filter);
                else if (masterVal === "05") exportExcelNewHire(filter);
                else if (masterVal === "06") exportExcelWfh(filter);     
                else if (masterVal === "07") exportExcelIncentives(filter);
                else if (masterVal === "08") exportExcelTerminated(filter);
            }
            else if (format === 'PDF') {
                if (masterVal === "01") exportPdf(filter);
                else if (masterVal === "02") exportPdfGratuity(filter);
                else if (masterVal === "03") exportPdfYearlyBonus(filter);
                else if (masterVal === "04") exportPdfExtraDay(filter);
                else if (masterVal === "05") exportPdfNewHire(filter);
                else if (masterVal === "06") exportPdfWfh(filter);       
                else if (masterVal === "07") exportPdfIncentives(filter);
                else if (masterVal === "08") exportPdfTerminated(filter);
            }
        });

        $('#btnPreviewPdf').off('click').on('click', function () {
            var filter = buildFilter();
            var masterVal = $("#masterFileTypeSelect").val();

            if (masterVal === "01") previewPdf(filter);
            else if (masterVal === "02") previewPdfGratuity(filter);
            else if (masterVal === "03") previewPdfYearlyBonus(filter);
            else if (masterVal === "04") previewPdfExtraDay(filter);
            else if (masterVal === "05") previewPdfNewHire(filter);
            else if (masterVal === "06") previewPdfWfh(filter);         
            else if (masterVal === "07") previewPdfIncentives(filter);
            else if (masterVal === "08") previewPdfTerminated(filter);
        });

        function setDateInputsDisabled(disabled) {
            $('#dateFrom, #dateTo').prop('disabled', disabled);
            $('#dateFrom, #dateTo').each(function () {
                if (this._flatpickr) {
                    if (this._flatpickr.altInput) {
                        $(this._flatpickr.altInput).prop('disabled', disabled).css('pointer-events', disabled ? 'none' : '');
                    }
                    if (this._flatpickr._input) {
                        $(this._flatpickr._input).prop('disabled', disabled);
                    }
                }
            });
            $('#dateFrom, #dateTo').closest('.col-9, .col-md-7').find('input').prop('disabled', disabled).css('pointer-events', disabled ? 'none' : '');
        }

        function toggleGenerateSections() {
            var mode = $('input[name="salaryGenerate"]:checked').val();
            var isByDate = mode === 'ByDate';

            // Always keep Date, Month, and Year fields visible
            $('#dateFrom, #dateTo, #monthSelect, #yearSelect').closest('.col-12').show();

            setDateInputsDisabled(!isByDate);
            $('#monthSelect, #yearSelect').prop('disabled', isByDate);

            if ($.fn.select2 && $('#monthSelect').data('select2')) {
                $('#monthSelect').prop('disabled', isByDate).trigger('change.select2');
            }

            var now = new Date();
            var currentMonth = now.getMonth() + 1;
            var currentYear = now.getFullYear();

            if (!$('#monthSelect').val()) {
                $('#monthSelect').val(currentMonth);
                if (!$('#monthSelect').val()) {
                    $('#monthSelect').val(String(currentMonth).padStart(2, '0'));
                }
                if ($.fn.select2 && $('#monthSelect').data('select2')) {
                    $('#monthSelect').trigger('change');
                }
            }
            if (!$('#yearSelect').val()) {
                $('#yearSelect').val(currentYear);
            }
        }

        $('input[name="salaryGenerate"]').on('change', toggleGenerateSections);
        toggleGenerateSections();

        settings.load();
    };
})(jQuery);