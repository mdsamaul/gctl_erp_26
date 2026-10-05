$(document).ready(function () {

   

    $('#js-leveSummery-report-clear').on('click', function () {
       

        // সব dropdown reset করো
        
        $('#departmentDropdown').val([]).selectpicker('refresh');
        $('#leaveFormatDropdown').val([]).selectpicker('refresh');
        $('#branchDropdown').val([]).selectpicker('refresh');
        $('#employeeDropdown').val([]).selectpicker('refresh');
        $('#leaveStatusDropdown').val([]).selectpicker('refresh');
        $('#reportFormatDropdown').val('');

        // date input reset করো
        $('#dateFromStr').val('');
        $('#dateToStr').val('');

        $('#dateFromStr').val(getCurrentDate());
        $('#dateToStr').val(getCurrentDate());
    });



    $('.js-leveSummery').on('click', function () {
        // Export button click হলে downloadBtn‑কে trigger করো
        $('#exportButtonNew').trigger('click');
    });


    $('.js-HomeOfficeRequest-report-favorite').on('click', function () {
        toastr.success('Favorite button Coming soon!');
    });
  

    $('#previewBtn2').click(function (e) {
        e.preventDefault();

        var formData = {
            company: $('#companyDropdown').val(),
            branch: $('#branchDropdown').val(),
            department: $('#departmentDropdown').val(),
            employee: $('#employeeDropdown').val(),
            dateFromStr: $('#dateFromStr').val(),
            dateToStr: $('#dateToStr').val(),
            leaveFormat: $('#leaveFormatDropdown').val(),
            leaveStatus: $('#leaveStatusDropdown').val()
        };

        if (!formData.company || formData.company.length === 0) {
            toastr.warning('Please select a company.');
            return;
        }

        $('#loadingSpinner1').show();
        $('#pdfPreviewContainer').hide();

        $.ajax({
            type: 'POST',
            url: '/LeaveReport/PreviewPdf',
            data: formData,
            success: function (response) {
                $('#loadingSpinner1').hide();
                if (!response.success) {
                    toastr.warning(response.message || 'Failed to generate preview.');
                    return;
                }
                $('#pdfPreview').attr('src', 'data:application/pdf;base64,' + response.pdfData);
                $('#pdfPreviewContainer').show();
                $('html, body').animate({ scrollTop: $('#pdfPreviewContainer').offset().top - 50 }, 500);
            },
            error: function (xhr) {
                $('#loadingSpinner1').hide();
                var msg = xhr.responseJSON?.message || 'An error occurred.';
                toastr.warning(msg);
            }
        });
    });



    // ─── INIT ────────────────────────────────────────────────────────────────

    getCompany();
    initializeSelectPicker('leaveFormatDropdown');
    initializeSelectPicker('leaveStatusDropdown');

    fetchDropdownData('/LeaveReport/GetBranchesMultiComp',        { companyCode: null, isAll: true }, 'branchDropdown',     'branchCode',     'branchName');
    fetchDropdownData('/LeaveReport/GetDeptMultiCompBranch',      { companyCode: null, branchCode: null, isAll: true }, 'departmentDropdown', 'departmentCode', 'departmentName');
    fetchDropdownData('/LeaveReport/GetEmpMultiCompBranchDept',   { companyCode: null, branchCode: null, departmentCode: null, isAll: true }, 'employeeDropdown', 'employeeId', 'employeeFirstName');

    // ─── DATEPICKER ──────────────────────────────────────────────────────────

    $(function () {
        $("#dateFromStr, #dateToStr").datepicker({ dateFormat: "dd/mm/yy" });
    });

    function getCurrentDate() {
        var today = new Date();
        return String(today.getDate()).padStart(2, '0') + '/'
             + String(today.getMonth() + 1).padStart(2, '0') + '/'
             + today.getFullYear();
    }

    $('#dateFromStr').val(getCurrentDate());
    $('#dateToStr').val(getCurrentDate());

    // ─── DROPDOWN CASCADE ────────────────────────────────────────────────────

    $('#companyDropdown').on('changed.bs.select', function () {
        var selected = $(this).val();
        var hasSelection = selected && selected.length > 0;

        fetchDropdownData('/LeaveReport/GetBranchesMultiComp',
            { companyCode: hasSelection ? selected : null, isAll: !hasSelection },
            'branchDropdown', 'branchCode', 'branchName');

        fetchDropdownData('/LeaveReport/GetDeptMultiCompBranch',
            { companyCode: hasSelection ? selected : null, branchCode: null, isAll: !hasSelection },
            'departmentDropdown', 'departmentCode', 'departmentName');

        fetchDropdownData('/LeaveReport/GetEmpMultiCompBranchDept',
            { companyCode: hasSelection ? selected : null, branchCode: null, departmentCode: null, isAll: !hasSelection },
            'employeeDropdown', 'employeeId', 'employeeFirstName');
    });

    $('#branchDropdown').change(function () {
        var compCode = $('#companyDropdown').val();
        var selected = $(this).val();
        var hasSelection = selected && selected.length > 0;

        fetchDropdownData('/LeaveReport/GetDeptMultiCompBranch',
            { companyCode: compCode, branchCode: hasSelection ? selected : null, isAll: !hasSelection },
            'departmentDropdown', 'departmentCode', 'departmentName');

        fetchDropdownData('/LeaveReport/GetEmpMultiCompBranchDept',
            { companyCode: compCode, branchCode: hasSelection ? selected : null, departmentCode: null, isAll: !hasSelection },
            'employeeDropdown', 'employeeId', 'employeeFirstName');
    });

    $('#departmentDropdown').change(function () {
        var compCode    = $('#companyDropdown').val();
        var branchCode  = $('#branchDropdown').val();
        var selected    = $(this).val();
        var hasSelection = selected && selected.length > 0;

        fetchDropdownData('/LeaveReport/GetEmpMultiCompBranchDept',
            { companyCode: compCode, branchCode: branchCode, departmentCode: hasSelection ? selected : null, isAll: !hasSelection },
            'employeeDropdown', 'employeeId', 'employeeFirstName');
    });

    // ─── COMPANY LOAD ────────────────────────────────────────────────────────

    function getCompany() {
        $.ajax({
            url: '/LeaveReport/GetCompanies',
            type: 'GET',
            success: function (response) {
                var $dd = $('#companyDropdown').empty();
                if (response.result && response.result.length > 0) {
                    $.each(response.result, function (i, com) {
                        $dd.append(`<option value="${com.companyCode}">${com.companyName}</option>`);
                    });
                }
                initializeSelectPicker('companyDropdown');

                if (response.firstData) {

                    $('#companyDropdown').val(response.firstData).trigger('change')
                }

            },
            error: function () {
                console.error('Error fetching companies');
                initializeSelectPicker('companyDropdown');
            }
        });
    }

    // ─── GENERIC DROPDOWN FETCH ──────────────────────────────────────────────

    function fetchDropdownData(endpoint, params, dropdownId, valueField, textField) {
        var $dd = $('#' + dropdownId);
        $dd.empty().append('<option value="">Loading...</option>');
        $dd.selectpicker('refresh');

        $.ajax({
            url: endpoint,
            type: 'GET',
            traditional: true,
            data: params,
            success: function (response) {
                $dd.empty();
                if (response.result && response.result.length > 0) {
                    response.result.forEach(function (item) {
                        $dd.append(`<option value="${item[valueField]}">${item[textField]} (${item[valueField]})</option>`);
                    });
                } else {
                    $dd.append('<option value="">No data available</option>');
                }
                initializeSelectPicker(dropdownId);
            },
            error: function () {
                $dd.empty().append('<option value="">Error loading data</option>');
                initializeSelectPicker(dropdownId);
            }
        });
    }

    // ─── SELECTPICKER ────────────────────────────────────────────────────────

    function initializeSelectPicker(elementId) {
        var $el = $('#' + elementId);
        if ($el.data('selectpicker')) $el.selectpicker('destroy');
        $el.selectpicker({
            liveSearch: true,
            liveSearchPlaceholder: 'Search...',
            size: 10,
            selectedTextFormat: 'count',
            actionsBox: true,
            iconBase: 'fa',
            showTick: true,
            tickIcon: 'fa-check',
            container: 'body'
        });
        $el.selectpicker('refresh');
    }

    // ─── EXPORT ──────────────────────────────────────────────────────────────

    $('#exportButtonNew').click(function (e) {
        e.preventDefault();

        var formData = {
            company:       $('#companyDropdown').val(),
            branch:        $('#branchDropdown').val(),
            department:    $('#departmentDropdown').val(),
            employee:      $('#employeeDropdown').val(),
            dateFromStr:   $('#dateFromStr').val(),
            dateToStr:     $('#dateToStr').val(),
            leaveFormat:   $('#leaveFormatDropdown').val(),
            leaveStatus:   $('#leaveStatusDropdown').val(),
            reportFormat:  $('#reportFormatDropdown').val()
        };

        if (!formData.company || !formData.reportFormat) {
            toastr.warning('Please select both Company and Report Format.');
            return;
        }

        toastr.info('Generating report...', 'Please wait', { timeOut: 3000, closeButton: true });

        $.ajax({
            type: 'POST',
            url: '/LeaveReport/DownloadReportN',  // <-- updated
            data: formData,
            xhrFields: { responseType: 'blob' },
            success: function (blob, status, xhr) {
                
                var contentType        = xhr.getResponseHeader('Content-Type');
                var contentDisposition = xhr.getResponseHeader('Content-Disposition');
                var filename = 'LeaveReport';

                if (contentDisposition) {
                    var matches = contentDisposition.match(/filename="?([^"]+)"?/);
                    if (matches && matches[1]) filename = matches[1];
                }

                if (contentType.includes('application/pdf')) {
                    filename += '.pdf';
                } else if (contentType.includes('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')) {
                    filename += '.xlsx';
                } else {
                    toastr.error('Unsupported file format received.');
                    return;
                }

                var blobUrl = window.URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = blobUrl;
                a.download = filename;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                window.URL.revokeObjectURL(blobUrl);

                toastr.success('Report downloaded successfully!');
            },
            error: function (res) {
               
                toastr.warning('"Error occurs"');
            }
        });
    });

});
