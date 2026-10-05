

$(document).ready(function () {


    // Initial setup


    getCompany();

    initializeSelectPicker('leaveFormatDropdown');
    initializeSelectPicker('leaveStatusDropdown');

    //#region Dropdowns

    fetchDropdownData('/LeaveApplicationEntry/GetBranchesMultiComp', { companyCode: null, isAll: true }, 'branchDropdown', 'branchCode', 'branchName');
    fetchDropdownData('/LeaveApplicationEntry/GetDeptMultiCompBranch', { companyCode: null, branchCode: null, isAll: true }, 'departmentDropdown', 'departmentCode', 'departmentName');
    fetchDropdownData('/LeaveApplicationEntry/GetEmpMultiCompBranchDept', { companyCode: null, branchCode: null, departmentCode: null, isAll: true }, 'employeeDropdown', 'employeeId', 'employeeFirstName');

    // Company dropdown change handler
    $('#companyDropdown').on('changed.bs.select', function () {
        var selectedCompanies = $(this).val();
        if (selectedCompanies && selectedCompanies.length > 0) {
            fetchDropdownData('/LeaveApplicationEntry/GetBranchesMultiComp', { companyCode: selectedCompanies, isAll: false }, 'branchDropdown', 'branchCode', 'branchName');
            fetchDropdownData('/LeaveApplicationEntry/GetDeptMultiCompBranch', { companyCode: selectedCompanies, branchCode: null, isAll: false }, 'departmentDropdown', 'departmentCode', 'departmentName');
            fetchDropdownData('/LeaveApplicationEntry/GetEmpMultiCompBranchDept', { companyCode: selectedCompanies, branchCode: null, departmentCode: null, isAll: false }, 'employeeDropdown', 'employeeId', 'employeeFirstName');


        } else {           
            fetchDropdownData('/LeaveApplicationEntry/GetBranchesMultiComp', { companyCode: null, isAll: true }, 'branchDropdown', 'branchCode', 'branchName');
            fetchDropdownData('/LeaveApplicationEntry/GetDeptMultiCompBranch', { companyCode: null, branchCode: null, isAll: true }, 'departmentDropdown', 'departmentCode', 'departmentName');
            fetchDropdownData('/LeaveApplicationEntry/GetEmpMultiCompBranchDept', { companyCode: null, branchCode: null, departmentCode: null, isAll: true }, 'employeeDropdown', 'employeeId', 'employeeFirstName');


        }
    });

    // Company dropdown change handler
    $('#branchDropdown').change(function () {       
        var compCode = $('#companyDropdown').val();
        var selectedBranches = $(this).val();
        if (selectedBranches && selectedBranches.length > 0) {
            fetchDropdownData('/LeaveApplicationEntry/GetDeptMultiCompBranch', { companyCode: compCode, branchCode: selectedBranches, isAll: false }, 'departmentDropdown', 'departmentCode', 'departmentName');
            fetchDropdownData('/LeaveApplicationEntry/GetEmpMultiCompBranchDept', { companyCode: compCode, branchCode: null, departmentCode: null, isAll: false }, 'employeeDropdown', 'employeeId', 'employeeFirstName');

        } else {
           

            fetchDropdownData('/LeaveApplicationEntry/GetDeptMultiCompBranch', { companyCode: null, branchCode: null, isAll: true }, 'departmentDropdown', 'departmentCode', 'departmentName');
            fetchDropdownData('/LeaveApplicationEntry/GetEmpMultiCompBranchDept', { companyCode: null, branchCode: null, departmentCode: null, isAll: true }, 'employeeDropdown', 'employeeId', 'employeeFirstName');



        }
    });

    // Company dropdown change handler
    $('#departmentDropdown').change(function () {
        
        var branchCode = $('#branchDropdown').val();
        var compCode = $('#companyDropdown').val();
        var selectedDepartments = $(this).val();
        if (selectedDepartments && selectedDepartments.length > 0) {
            //getDepartments(selectedBranches, false);
            fetchDropdownData('/LeaveApplicationEntry/GetEmpMultiCompBranchDept', { companyCode: compCode, branchCode: branchCode, departmentCode: selectedDepartments, isAll: false }, 'employeeDropdown', 'employeeId', 'employeeFirstName');

        } else {
            
            fetchDropdownData('/LeaveApplicationEntry/GetEmpMultiCompBranchDept', { companyCode: null, branchCode: null, departmentCode: null, isAll: true }, 'employeeDropdown', 'employeeId', 'employeeFirstName');

        }
    });

    function getCompany() {
        $.ajax({
            url: '/LeaveApplicationEntry/GetCompanies',
            type: 'GET',
            success: function (company) {
                if (company.result && company.result.length > 0) {
                    var $companyDropdown = $('#companyDropdown');
                    $companyDropdown.empty();
                    
                    $.each(company.result, function (index, com) {
                        $companyDropdown.append(
                            `<option value="${com.companyCode}">${com.companyName}</option>`
                        );
                    });
                    
                    initializeSelectPicker('companyDropdown');
                }
            },
            error: function (xhr, status, error) {
                console.error('Error fetching companies:', error);
                var $companyDropdown = $('#companyDropdown');
                $companyDropdown.empty();
                $companyDropdown.append('<option value="">Error loading companies</option>');
                initializeSelectPicker('companyDropdown');
            }
        });
    }


    function fetchDropdownData(endpoint, params, dropdownId, valueField, textField) {
        var $dropdown = $('#' + dropdownId);
        $dropdown.empty();
        $dropdown.append('<option value="">Loading...</option>');
        $dropdown.selectpicker('refresh');

        $.ajax({
            url: endpoint,
            type: 'GET',
            traditional: true,
            data: params,
            success: function (response) {
                $dropdown.empty();

                //console.log('this is from fetchDDdata :: ', response)

                if (response.result && response.result.length > 0) {
                    response.result.forEach(function (item) {
                        $dropdown.append(`<option value="${item[valueField]}">${item[textField]} (${item[valueField]})</option>`);
                    });
                } else {
                    $dropdown.append('<option value="">No data available</option>');
                }

                initializeSelectPicker(dropdownId);
            },
            error: function (xhr, status, error) {
                console.error('Error fetching data:', error);
                $dropdown.empty();
                $dropdown.append('<option value="">Error loading data</option>');
                initializeSelectPicker(dropdownId);
            }
        });
    }

    function initializeSelectPicker(elementId) {
        var $element = $('#' + elementId);
        
        // Destroy if exists
        if ($element.data('selectpicker')) {
            $element.selectpicker('destroy');
        }
        
        // Initialize with options
        $element.selectpicker({
            liveSearch: true,
            enableSelectedText: true,
            liveSearchPlaceholder: 'Search...',
            size: 10,
            selectedTextFormat: 'count',
            actionsBox: true,
            iconBase: 'fa',
            showTick: true,
            tickIcon: 'fa-check',
            container: 'body'
        });
        
        // Refresh to ensure proper rendering
        $element.selectpicker('refresh');
    }

    //#endregion dropdowns

    // #region DatePicker

    $(function () {
        $("#dateFrom, #dateFromStr, #dateTo, #dateToStr").datepicker({
            dateFormat: "dd/mm/yy"
        });
    });

    function getCurrentDate() {
        const today = new Date();
        const day = String(today.getDate()).padStart(2, '0');
        const month = String(today.getMonth() + 1).padStart(2, '0'); // Months are zero-based
        const year = today.getFullYear();
        return day + '/' + month + '/' + year;
    }

    // Set current date to the input fields
    $('#dateFromStr').val(getCurrentDate());
    $('#dateToStr').val(getCurrentDate());

    //#endregion

    //#region Export And Previw



    //$('#previewButton').click(function (e) {
    //    e.preventDefault();
    //    submitPreviewForm();
    //});

    //$('#previewBtn').click(function (e) {
    //    e.preventDefault();
    //    generatePreview();
    //});



    //function generatePreview() {
    //    // Show loading spinner
    //    $('#loadingSpinner').show();
    //    $('#pdfPreviewContainer').hide();

    //    // Serialize form data
    //    const formData = $('#leaveApplicationForm').serialize();
    //    console.log('formData for PreviewLeaveReportArray', formData);

    //    $.ajax({
    //        url: '/LeaveApplicationEntry/PreviewLeaveReport',
    //        type: 'POST',
    //        data: formData,
    //        success: function (response) {
    //            //  console.log('PreviewLeaveReport', response);
    //            // Hide loading spinner
    //            $('#loadingSpinner').hide();

    //            // Update PDF preview
    //            var pdfPreview = $('#pdfPreview');
    //            pdfPreview.attr('src', 'data:application/pdf;base64,' + response.pdfData);

    //            // Show preview container
    //            $('#pdfPreviewContainer').show();

    //            // Smooth scroll to preview
    //            $('html, body').animate({
    //                scrollTop: $('#pdfPreviewContainer').offset().top - 50
    //            }, 500);
    //        },
    //        error: function (xhr, status, error) {
    //            // Hide loading spinner
    //            $('#loadingSpinner').hide();

    //            // Show error message
    //            Swal.fire({
    //                icon: 'error',
    //                title: 'Preview Generation Failed',
    //                text: 'Failed to generate preview. Please try again.',
    //                confirmButtonText: 'OK'
    //            });
    //        }
    //    });
    //}




    $('#exportButton').click(function (e) {
        e.preventDefault();
        // if (!validateForm()) return;  // Optional form validation

        // Create form for submission
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = '/LeaveApplicationEntry/DownloadReport';

        // Add form fields
        const formData = {
            company: $('#companyDropdown').val(),
            branch: $('#branchDropdown').val(),
            department: $('#departmentDropdown').val(),
            employee: $('#employeeDropdown').val(),
            dateFrom: $('#dateFrom').val(),
            dateFromStr: $('#dateFromStr').val(),
            dateTo: $('#dateTo').val(),
            dateToStr: $('#dateToStr').val(),
            leaveFormat: $('#leaveFormatDropdown').val(),
            leaveStatus: $('#leaveStatusDropdown').val(),
            reportFormat: $('#reportFormatDropdown').val()
        };

        // Add form fields as hidden inputs
        Object.keys(formData).forEach(key => {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = key;
            input.value = formData[key] || '';
            form.appendChild(input);
        });

        // Show loading message
        toastr.info('Generating report...', 'Please wait', {
            timeOut: 0,
            extendedTimeOut: 0,
            closeButton: false
        });

        // Submit form using AJAX
        $.ajax({
            type: 'POST',
            url: form.action,
            data: formData,
            success: function (response) {
                console.log(response)
                if (response.success) {

                    toastr.warning(response.message || 'Something went wrong22');

                } else {
                    // Report generation failure
                    toastr.success('Report generation started11');

                    // Submit form
                    document.body.appendChild(form);
                    form.submit();
                    document.body.removeChild(form);

                    // Trigger file download or any other actions here

                }
            },
            error: function () {
                // Handle unexpected errors

                toastr.warning('An error occurred while generating the report33.');
            }
        });

        // Clear the loading message after some time
        setTimeout(() => {
            toastr.clear();
        }, 1000);
    });


  

    $('#exportButtonNew').click(function (e) {
        e.preventDefault();

        // Gather form data
        const formData = {
            company: $('#companyDropdown').val(),
            branch: $('#branchDropdown').val(),
            department: $('#departmentDropdown').val(),
            employee: $('#employeeDropdown').val(),
            dateFrom: $('#dateFrom').val(),
            dateFromStr: $('#dateFromStr').val(),
            dateTo: $('#dateTo').val(),
            dateToStr: $('#dateToStr').val(),
            leaveFormat: $('#leaveFormatDropdown').val(),
            leaveStatus: $('#leaveStatusDropdown').val(),
            reportFormat: $('#reportFormatDropdown').val()
        };

        // Validate required fields (company and reportFormat)
        if (!formData.company || !formData.reportFormat) {
            toastr.warning('Please select both Company and Report Format.');
            return;
        }


        // Show loading message
        toastr.info('Generating report...', 'Please wait', {
            timeOut: 3000,
            closeButton: true
        });

        // Send AJAX request
        $.ajax({
            type: 'POST',
            url: '/LeaveApplicationEntry/DownloadReportN',
            data: formData,
            xhrFields: {
                responseType: 'blob' // Handle binary data (Excel or PDF)
            },
            success: function (blob, status, xhr) {

              


                const contentType = xhr.getResponseHeader('Content-Type');
                const contentDisposition = xhr.getResponseHeader('Content-Disposition');
                let filename = 'LeaveReport';

                // Extract filename from response headers
                if (contentDisposition) {
                    const matches = contentDisposition.match(/filename="?([^"]+)"?/);
                    if (matches && matches[1]) filename = matches[1];
                }

                // Determine file extension based on content type
                if (contentType.includes('application/pdf')) {
                    filename += '.pdf';
                } else if (contentType.includes('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')) {
                    filename += '.xlsx';
                } else {
                    toastr.error('Unsupported file format received.');
                    return;
                }

                // Create a Blob URL and trigger download
                const blobUrl = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = blobUrl;
                a.download = filename;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                window.URL.revokeObjectURL(blobUrl);

                toastr.success('Report downloaded successfully!');
            },
            error: function ( xhr) {

              
                

                if (xhr.status === 400 || xhr.status === 500 || xhr.status === 200) {
                    //xhr.response
                    //    .text()
                    //    .then(errorMessage => {
                    //        toastr.error(errorMessage);
                    //    })
                    //    .catch(() => toastr.error('An error occurred while generating the report.'));

                    toastr.warning('An unexpected error occurred.');

                } else {
                    toastr.warning('An unexpected error occurred.');
                }
            }
        });
    });




    

    //#endregion

});






