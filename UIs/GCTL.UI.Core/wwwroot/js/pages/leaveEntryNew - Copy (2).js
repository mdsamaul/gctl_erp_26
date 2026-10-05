$(document).ready(function () {



    initializeDataTable();
    fetchEntryId();

    $("#fromDate, #toDate").change(function () {
        validateDates();

    });

    $("#fromTime, #toTime").change(function () {
        validateTimes();
    });
    selectTo();

    function selectTo() {
        $('#leaveApplicationForm' + ' .selectpickerLeave').select2({
            language: {
                noResults: function () {

                }
            },
            escapeMarkup: function (markup) {
                return markup;
            }
        });
    }


    var $dropdown = $("#companyDropdown");
    if ($dropdown.find("option").length === 2) {
        // দ্বিতীয় option select করো
        $dropdown.prop("selectedIndex", 1).trigger("change");

        var companyCode = $("#companyDropdown").val();
        if (companyCode) {
            initEmployeeSelect2(companyCode);
        }
    }

    //$dropdown.on("change", function () {
    //    
    //});







    // $("#fromDate, #toDate").on("change", function() {
    //        var dateValue = $(this).val(); // This is in yyyy-MM-dd format
    //        if (dateValue) {
    //            // Store the formatted display date in a data attribute
    //            var date = new Date(dateValue);
    //            var displayDate = date.getDate().toString().padStart(2, '0') + '/' +
    //                            (date.getMonth() + 1).toString().padStart(2, '0') + '/' +
    //                            date.getFullYear();

    //            $(this).attr('data-display', displayDate);
    //            // Keep the original yyyy-MM-dd format in the actual value
    //            $(this).val(dateValue);
    //        }
    //    });

    ///-------

    // Employee Dropdown Change Event
    $('#employeeDropdown').on('change', function () {
        var selectedEmpId = $(this).val();
        if (selectedEmpId) {
            $.ajax({
                //url: '@Url.Action("GetEmployeeLeaveTaken", "LeaveApplicationEntry")',
                url: '/LeaveApplicationEntry/GetEmployeeLeaveTaken',
                type: 'GET',
                data: { EmpId: selectedEmpId },
                success: function (response) {
                    if (response.success) {
                        console.log('Approve Leave List ---', response)
                        EmployeeLeaveTaken(response.data.result)

                    } else {
                        // alert('Failed to fetch employee RestInfo details.');
                        toastr.warning('Failed to fetch employee RestInfo details.', 'Message');

                    }
                },
                error: function () {
                    toastr.warning('Error fetching employee details.', 'Message');

                    //alert('Error fetching employee details.');
                }
            });
        } else {
            // Clear fields if no employee selected
            clearEmployeeFields();
        }
    });


    //Approved leave
    function EmployeeLeaveTaken(data) {
        console.log('EmployeeLeaveTaken Func:', data); // Confirm the data being passed

        $('#leaveTakenTable').DataTable({
            destroy: true, // Reinitialize if the table already exists
            data: data, // Pass the array of objects directly
            columns: [
                {
                    data: 'startDate',
                    render: function (data) {
                        return data ? new Date(data).toLocaleDateString() : '';
                    },
                    title: 'Start Date',
                    width: '10%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'endDate',
                    render: function (data) {
                        return data ? new Date(data).toLocaleDateString() : '';
                    },
                    title: 'End Date',
                    width: '10%',
                    className: 'dt-custom-padding'
                },


                {
                    data: 'days',
                    render: function (data) {
                        if (Array.isArray(data)) {
                            return '<ul style="list-style-type: none;  margin: 0;   padding: 0;">' + data.map(date => `<li>${date.split('T')[0]}</li>`).join('') + '</ul>';
                        } else if (data === null) {
                            return '';
                        } else if (typeof data === 'string' && data.includes('T')) {
                            return `<ul><li>${data.split('T')[0]}</li></ul>`;
                        }
                        return data;
                    },
                    width: '16%',
                    className: 'dt-custom-padding'
                },

                {
                    data: 'noOfDay',
                    title: 'No of Days',
                    width: '8%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'reason', // Correct key
                    title: 'Reason',
                    width: '30%',
                    render: function (data) {
                        return data !== null && data !== undefined ? data : 'N/A';
                    },
                    className: 'dt-custom-padding'
                }
            ],
            scrollY: '268px', // Set height of the table body
            scrollCollapse: true, // Allow the table to shrink if data is less
            autoWidth: true,   // Disable auto width for more control
            responsive: true,   // Make the table responsive
            searching: true,   // Disable search (optional)
            paging: true,      // Disable pagination (optional)
            info: false         // Disable "Showing X of Y entries" text (optional)
        });

        // $('#leaveTakenTable th').css('padding', '.3rem');
        // $('#leaveTakenTable td').css('padding', '.3rem');

    }



    // Employee Dropdown Change Event
    $('#employeeDropdown').on('change', function () {
        var selectedEmpId = $(this).val();
        if (selectedEmpId) {
            $.ajax({
                url: '/LeaveApplicationEntry/GetEmployeeLeaveBalance',
                type: 'GET',
                data: { EmpId: selectedEmpId },
                success: function (response) {
                    if (response.success) {
                        console.log('GetEmployeeLeaveRestInfo', response)
                        LeaveBalance(response.data)

                    } else {
                        toastr.warning('Failed to fetch employee RestInfo details.', 'Message');

                        //alert('Failed to fetch employee RestInfo details.');
                    }
                },
                error: function () {
                    toastr.warning('Error fetching employee details.', 'Message');

                    //alert('Error fetching employee details.');
                }
            });
        } else {
            // Clear fields if no employee selected
            clearEmployeeFields();
        }
    });


    //Leave Status
    function LeaveBalance(data) {
        console.log('LeaveBalance Func:', data);
        $('#leaveAvailableTable').DataTable({
            destroy: true,
            data: data,
            columns: [
                {
                    data: 'shortName',
                    title: 'LN',
                    width: '15%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'availableLeave',
                    title: 'YLE',
                    width: '20%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'totalLeaveTaken',
                    title: 'TAK',
                    width: '20%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'remainingLeave',
                    title: 'BAL',
                    width: '20%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'totalShortLeaveTimeHour',
                    title: 'Sh.L',
                    width: '15%',
                    className: 'dt-custom-padding',
                    render: function (data) {
                        return data !== null && data !== undefined ? data : 'N/A';
                    }
                }
            ],
            scrollY: '400px',
            scrollCollapse: true,
            autoWidth: true,
            responsive: true,
            searching: false,
            paging: false,
            info: false,
            ordering: false
        });
    }


    const isEditMode = window.location.pathname.toLowerCase().includes('edit');
    const leaveAppEntryCode = new URLSearchParams(window.location.search).get('id');

    if (isEditMode && leaveAppEntryCode) {
        loadLeaveApplication(leaveAppEntryCode);
    }

    // Load existing leave application data
    function loadLeaveApplication(id) {
        $.ajax({
            url: '/LeaveApplicationEntry/GetLeaveApplication1',
            type: 'GET',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    populateForm(response.data);
                } else {
                    toastr.warning('Error fetching employee details.', 'Message');

                    // alert('Failed to load leave application.');
                }
            },
            error: function () {
                toastr.warning('Error fetching employee details.', 'Message');

                //alert('Error loading leave application.');
            }
        });
    }

    //----------------- Populate form with existing data
    function populateForm(data) {
        // Basic fields
        $('#leaveAppEntryCode').val(data.leaveAppEntryCode);
        $('#entryId').val(data.leaveAppEntryId);
        $('#companyDropdown').val(data.companyCode).trigger('change');

        //// Wait for employees to load before setting employee
        //setTimeout(() => {
        //    $('#employeeDropdown').val(data.employeeId).trigger('change');

        //    // Wait for employee details to load before setting supervisor and HOD
        //    setTimeout(() => {
        //        $('#immediateSupervisor').val(data.bossEmpAutoId);
        //        $('#headOfDepartment').val(data.hod);
        //    }, 1000);
        //}, 500);

        setTimeout(() => {
            var empId = data.employeeId;
            var empName = data.employeeId + ' - ' + (data.employeeFirstName || '');

            // Pre-inject the option so Select2 can select it without an extra fetch
            var $emp = $('#employeeDropdown');
            if ($emp.find('option[value="' + empId + '"]').length === 0) {
                $emp.append(new Option(empName, empId, true, true));
            }
            $emp.val(empId).trigger('change');

            setTimeout(() => {
                $('#immediateSupervisor').val(data.bossEmpAutoId);
                $('#headOfDepartment').val(data.hod);
            }, 1000);
        }, 500);

        $('#leaveTypeDropdown').val(data.leaveTypeId);
        $('textarea[name="Reason"]').val(data.reason);

        var appleva = '';

        debugger
        // Format specific data population
        switch (data.applyLeaveFormat) {
            case 'FullLeave':
                if (data.startDate) {
                    $('#fromDate').val(data.startDate.split('T')[0]);
                }
                if (data.endDate) {
                    $('#toDate').val(data.endDate.split('T')[0]);
                }
                $('#daysBetween').val(data.noOfDay);

                appleva = 'fullDayLeave';

                // Clear existing calendar events
                calendar.getEvents().forEach(event => event.remove());
                selectedDates = [];

                // Add the days to calendar
                if (data.days && data.days.length > 0) {
                    data.days.forEach(date => {
                        const formattedDate = date.split('T')[0];
                        selectedDates.push(formattedDate);
                        calendar.addEvent({
                            title: 'Selected',
                            start: formattedDate,
                            allDay: true,
                            backgroundColor: '#28a745',
                            borderColor: '#28a745'
                        });
                    });
                }

                // Show calendar container
                $('#calendarContainer').show();
                // Force calendar to re-render to show selected dates
                setTimeout(() => {
                    calendar.render();
                    calendar.updateSize();
                }, 100);
                break;



            case 'HalfDayLeave':
                $('#halfDayCheckbox').prop('checked', data.halfDay === "1");
                if (data.firstOrSecondHalf === "1") {
                    $('#firstHalf').prop('checked', true);
                } else if (data.firstOrSecondHalf === "2") {
                    $('#secondHalf').prop('checked', true);
                }
                appleva = 'halfDayLeave';
                break;

            case 'ShortLeave':
                if (data.shortLeaveFrom) {
                    // Extract only the time portion and format it to HH:mm
                    const fromTime = data.shortLeaveFrom.split('T')[1].substring(0, 5);
                    $('#fromTime').val(fromTime);
                }
                if (data.shortLeaveTo) {
                    // Extract only the time portion and format it to HH:mm
                    const toTime = data.shortLeaveTo.split('T')[1].substring(0, 5);
                    $('#toTime').val(toTime);
                }
                appleva = 'shortLeave';
                break;
        }

        $('#applyLeaveFormat').val(appleva).trigger('change');


        // If there's a file path, show it
        if (data.sickLeaveFilePath) {
            // You might want to add a file preview element
            const fileName = data.sickLeaveFilePath.split('/').pop();
            // Add this HTML somewhere in your form to show the existing file
            $('#leaveFile').after(`<div class="mt-2"><small>Current file: ${fileName}</small></div>`);
        }
    }

    //------------------------------------------

    function validateTimes() {
        var fromTime = $("#fromTime").val();
        var toTime = $("#toTime").val();

        if (fromTime && toTime) {
            // Convert times to Date objects for comparison
            var from = new Date("1970-01-01T" + fromTime + "Z");
            var to = new Date("1970-01-01T" + toTime + "Z");

            if (from > to) {
                toastr.warning("The 'From' time must be earlier than the 'To' time.", 'Message');

                // alert("The 'From' time must be earlier than the 'To' time.");
                $("#fromTime").get(0).setCustomValidity("From time must be before To time.");
                $("#toTime").get(0).setCustomValidity("");
                $('#toTime').val('');
            } else {
                $("#fromTime").get(0).setCustomValidity("");
                $("#toTime").get(0).setCustomValidity("");
            }
        }
    }

    //----------------------------

    function validateDates() {
        var fromDate = $("#fromDate").val();
        var toDate = $("#toDate").val();

        if (fromDate && toDate) {
            var from = new Date(fromDate);
            var to = new Date(toDate);

            if (from > to) {
                toastr.warning("The 'From' date must be greater than the 'To' date.", 'Message');

                //alert("The 'From' date must be greater than the 'To' date.");
                $("#fromDate").get(0).setCustomValidity("From date must be before To date.");
                $("#toDate").get(0).setCustomValidity("");
                $('#toDate').val('');
            } else {
                $("#fromDate").get(0).setCustomValidity("");
                $("#toDate").get(0).setCustomValidity("");
            }
        }
    }


    //-----------------------------


    function fetchEntryId() {
        $.ajax({
            url: '/LeaveApplicationEntry/GenerateEntryId', // Replace with your actual controller name
            type: 'GET',
            success: function (response) {
                // Set the entryId to the input field
                $('#entryId').val(response.entryId.result);
                console.log('new entry Id : ', response)
            },
            error: function (xhr, status, error) {
                console.error('Error fetching entryId:', error);
            }
        });
    }





    //-------------------------


    let leaveApplicationTable;


    //function initializeDataTable() {
    //    // If table already exists, destroy it first
    //    if ($.fn.DataTable.isDataTable('#leaveApplicationsTable')) {
    //        $('#leaveApplicationsTable').DataTable().destroy();
    //    }


    //    $('#leaveApplicationsTable').DataTable({

    //        ajax: {
    //            url: '/LeaveApplicationEntry/Grid',
    //            type: 'GET',
    //            dataSrc: '',
    //            error: function (xhr, error, thrown) {
    //                console.error('DataTables Ajax Error:', error, thrown);
    //                $('#leaveApplicationsTable_wrapper')
    //                    .prepend('<div class="alert alert-danger">Unable to load leave applications. Please try refreshing the page or contact support if the problem persists.</div>');
    //            },
    //            // success: function(data) {
    //            //     console.log('Data retrieved from server:', data); // Log the data to the console
    //            // }
    //        },
    //        columns: [
    //            // { data: 'employeeID' },
    //            {
    //                data: 'leaveAppEntryCode',
    //                render: function (data) {
    //                    console.log()
    //                    return `<button onclick="editLeaveApplication('${data}')" class="btn btn-primary btn-sm mb-1">
    //                        <i class="fas fa-edit"></i>
    //                    </button>
    //                    <button onclick="deleteLeaveApplication('${data}')" class="btn btn-danger btn-sm">
    //                        <i class="fas fa-trash"></i>
    //                    </button>

    //                    `;
    //                }
    //            },
    //            {
    //                data: 'leaveAppEntryId',
    //                width: '5%'
    //            },
    //            // { data: 'hodFirstName' },
    //            {
    //                data: 'applyLeaveFormat',
    //                width: '5%'
    //            },
    //            {
    //                data: 'hodApprovalStatus',
    //                width: '5%'
    //            },

    //            {
    //                data: 'employeeID',
    //                width: '5%'
    //            },

    //            {
    //                data: 'employeeFirstName',
    //                width: '10%'
    //            },
    //            {
    //                data: 'leaveTypeId',
    //                width: '5%'
    //            },

    //            {
    //                data: 'sickLeaveFilePath',
    //                render: function (data) {
    //                    //console.log('sickLeaveFilePath', data);
    //                    if (data === null || data === '') {
    //                        return '<span>No file</span>';
    //                    }
    //                    return `
    //                            <button onclick="downloadLeaveApplication('${data}')" class="btn btn-info btn-sm">
    //                                <i class="fas fa-download"></i>
    //                            </button>
    //                        `;
    //                },
    //                width: '5%'
    //            },


    //            {
    //                data: 'startDate',
    //                render: function (data) {
    //                    return data ? new Date(data).toLocaleDateString() : '';
    //                },
    //                width: '5%'
    //            },
    //            {
    //                data: 'endDate',
    //                render: function (data) {
    //                    return data ? new Date(data).toLocaleDateString() : '';
    //                },
    //                width: '5%'
    //            },

    //            // {
    //            //     data: 'days',
    //            //     render: function (data) {
    //            //     if (Array.isArray(data)) {
    //            //         return data.map(date => date.split('T')[0]).join(', ');
    //            //     } else if (data === null) {
    //            //         return '';
    //            //     } else if (typeof data === 'string' && data.includes('T')) {
    //            //         return data.split('T')[0];
    //            //     }
    //            //     return data;
    //            //     }
    //            // },


    //            {
    //                data: 'days',
    //                render: function (data) {
    //                    if (Array.isArray(data)) {
    //                        return '<ul style="list-style-type: none;  margin: 0;   padding: 0;">' + data.map(date => `<li>${date.split('T')[0]}</li>`).join('') + '</ul>';
    //                    } else if (data === null) {
    //                        return '';
    //                    } else if (typeof data === 'string' && data.includes('T')) {
    //                        return `<ul><li>${data.split('T')[0]}</li></ul>`;
    //                    }
    //                    return data;
    //                },
    //                width: '10%'
    //            },



    //            {
    //                data: 'noOfDay',
    //                width: '5%'
    //            },
    //            {
    //                data: 'reason',
    //                render: function (data) {
    //                    return formatApprovalStatus(data);
    //                },
    //                width: '15%'
    //            },

    //            // { data: 'supervisorFirstName' },

    //            // {
    //            //     data: 'hrApprovalStatus',
    //            //     render: function (data) {
    //            //         return formatApprovalStatus(data);
    //            //     }
    //            // },
    //            {
    //                data: null,
    //                render: function (data, type, row, meta) {
    //                    return meta.row + 1; // Adds serial number starting from 1
    //                },
    //                width: '5%'
    //            }

    //        ],
    //        processing: true,
    //        serverSide: false,
    //        language: {
    //            processing: "Loading leave applications...",
    //            zeroRecords: "No leave applications found",
    //            emptyTable: "No leave applications available"
    //        },
    //        order: [[5, 'desc']],
    //        responsive: true,
    //        autoWidth: false


    //    });

    //}


    function initializeDataTable() {
        if ($.fn.DataTable.isDataTable('#leaveApplicationsTable')) {
            $('#leaveApplicationsTable').DataTable().destroy();
        }
        debugger
        leaveApplicationTable = $('#leaveApplicationsTable').DataTable({
            processing: true,
            serverSide: true,                  // ← enable server-side
            ajax: {
                url: '/LeaveApplicationEntry/Grid',
                type: 'GET',
                data: function (d) {
                    // DataTables sends: draw, start, length, search[value], order, columns
                    // We flatten search to a simpler param for the controller
                    return {
                        draw: d.draw,
                        start: d.start,
                        length: d.length,
                        searchValue: d.search.value
                    };
                },
                error: function (xhr, error, thrown) {
                    console.error('DataTables Ajax Error:', error, thrown);
                    $('#leaveApplicationsTable_wrapper')
                        .prepend('<div class="alert alert-danger">Unable to load leave applications.</div>');
                }
            },
            columns: [
                {
                    data: 'leaveAppEntryCode',
                    orderable: false,
                    searchable: false,
                    render: function (data) {
                        return `
                        <button onclick="editLeaveApplication('${data}')" class="btn btn-primary btn-sm mb-1">
                            <i class="fas fa-edit"></i>
                        </button>
                        <button onclick="deleteLeaveApplication('${data}')" class="btn btn-danger btn-sm">
                            <i class="fas fa-trash"></i>
                        </button>`;
                    }
                },
                { data: 'leaveAppEntryId', width: '5%' },
                { data: 'applyLeaveFormat', width: '5%' },
                { data: 'hodApprovalStatus', width: '5%' },
                { data: 'employeeID', width: '5%' },
                { data: 'employeeFirstName', width: '10%' },
                { data: 'leaveTypeId', width: '5%' },
                {
                    data: 'sickLeaveFilePath',
                    orderable: false,
                    searchable: false,
                    render: function (data) {
                        if (!data) return '<span>No file</span>';
                        return `<button onclick="downloadLeaveApplication('${data}')" class="btn btn-info btn-sm">
                                <i class="fas fa-download"></i>
                            </button>`;
                    },
                    width: '5%'
                },
                {
                    data: 'startDate',
                    render: function (data) { return data ? new Date(data).toLocaleDateString() : ''; },
                    width: '5%'
                },
                {
                    data: 'endDate',
                    render: function (data) { return data ? new Date(data).toLocaleDateString() : ''; },
                    width: '5%'
                },
                {
                    data: 'days',
                    orderable: false,
                    searchable: false,
                    render: function (data) {
                        if (Array.isArray(data)) {
                            return '<ul style="list-style-type:none;margin:0;padding:0;">'
                                + data.map(d => `<li>${d.split('T')[0]}</li>`).join('')
                                + '</ul>';
                        }
                        if (!data) return '';
                        if (typeof data === 'string' && data.includes('T')) {
                            return `<ul><li>${data.split('T')[0]}</li></ul>`;
                        }
                        return data;
                    },
                    width: '10%'
                },
                { data: 'noOfDay', width: '5%' },
                {
                    data: 'reason',
                    render: function (data) { return formatApprovalStatus(data); },
                    width: '15%'
                },
                {
                    data: null,
                    orderable: false,
                    searchable: false,
                    render: function (data, type, row, meta) {
                        return meta.row + 1;
                    },
                    width: '5%'
                }
            ],
            language: {
                processing: "Loading leave applications...",
                zeroRecords: "No leave applications found",
                emptyTable: "No leave applications available"
            },
            order: [[5, 'desc']],
            responsive: true,
            autoWidth: false
        });
    }


    function reloadDataTable() {
        if (leaveApplicationTable) {
            leaveApplicationTable.ajax.reload(null, false);
        } else {
            initializeDataTable();
        }
    }

    //function reloadDataTable() {
    //    if (leaveApplicationTable) {
    //        leaveApplicationTable.ajax.reload(null, false);
    //    } else {
    //        initializeDataTable();
    //    }
    //}





    // Define the function globally
    window.downloadLeaveApplication = function (id) {
        // Show a confirmation dialog
        //   var confirmation = confirm('Are you sure you want to download this leave application?');

        // If the user clicks "OK", proceed with the download
        // if (confirmation) {
        console.log('Download leave application with ID:', id);

        // Perform an AJAX GET request to fetch data or perform an action
        $.ajax({
            url: `/LeaveApplicationEntry/DownloadLeaveApplication`,
            type: 'GET',
            data: { url: id },
            success: function (response) {
                // Handle the successful response
                console.log('Response:', response);

                // Assuming the response contains a URL to download the file
                // You can create a link and trigger a download
                const downloadUrl = response.downloadUrl; // Adjust based on your response structure
                if (downloadUrl) {
                    window.open(downloadUrl, '_blank'); // Redirect to the download URL
                } else {
                    toastr.warning('Download link not available.', 'Message');

                    //alert('Download link not available.');
                }
            },
            error: function (xhr, status, error) {
                // Handle the error response
                console.error('Error:', error);
                toastr.warning('Failed to download leave application. Please try again.', 'Message');

                //alert('Failed to download leave application. Please try again.');
            }
        });
        // } else {
        //      // User clicked "Cancel", do nothing
        //      console.log('Download canceled by user.');
        //  }
    };







    // Define the function globally
    window.deleteLeaveApplication = function (id) {
        // Show a confirmation dialog
        var confirmation = confirm('Are you sure you want to delete this leave application?');

        // If the user clicks "OK", proceed with the deletion
        if (confirmation) {
            console.log('Deleting leave application with ID:', id);

            // Perform an AJAX GET request to fetch data or perform an action
            $.ajax({
                url: `/LeaveApplicationEntry/DeleteLeaveApplication`,
                type: 'GET',
                data: { id: id },
                success: function (response) {
                    // Handle the successful response
                    console.log('Response: delete', response);
                    toastr.success(response.message, 'Success');
                    // alert('Leave application deleted successfully.');
                    //window.location.href = `/LeaveApplicationEntry/Create`
                    reloadDataTable();
                    fetchEntryId();
                },
                error: function (xhr, status, error) {
                    // Handle the error response
                    console.error('Error:', error);
                    // alert('Failed to delete leave application. Please try again.');
                    toastr.warning('Failed to delete leave application. Please try again.', 'Message');
                }
            });
        } else {
            // User clicked "Cancel", do nothing
            console.log('Deletion canceled by user.');
        }
    };







    // Define the function globally
    window.editLeaveApplication = function (id) {
        console.log('Editing leave application with ID:', id);

        // Perform an AJAX GET request to fetch data or perform an action
        $.ajax({
            url: `/LeaveApplicationEntry/GetLeaveApplication1`,
            type: 'GET',
            data: { id: id },
            success: function (response) {


                // Handle the successful response
                console.log('Response:', response);


                populateForm(response.data);


            },
            error: function (xhr, status, error) {
                // Handle the error response
                console.error('Error:', error);
                toastr.warning('Failed to fetch leave application details. Please try again.', 'Message');

                //alert('Failed to fetch leave application details. Please try again.');
            }
        });
    };



    function formatApprovalStatus(status) {
        if (!status) return '';

        const statusClasses = {
            'Approved': 'text-success',
            'Pending': 'text-warning',
            'Rejected': 'text-danger'
        };

        const className = statusClasses[status] || 'text-secondary';
        return `<span class="${className}">${status}</span>`;
    }





    //------------------------

    // Initialize - hide all format specific inputs
    $("#fullDayInputs, #halfDayInputs, #oneDayInputs").hide();  /* , #calendarContainer */

    // Company Dropdown Change Event
    $('#companyDropdown').on('change', function () {
        var companyCode = $(this).val();
        if (companyCode) {
            initEmployeeSelect2(companyCode);
        } else {
            $('#employeeDropdown').empty().append('<option value="">--Select Employee--</option>');
        }
    });


    // ── Employee dropdown: Select2 with AJAX infinite scroll ──────────────────

    function initEmployeeSelect2(companyCode) {
        var $emp = $('#employeeDropdown');

        // Destroy any previous instance
        if ($emp.data('select2')) {
            $emp.select2('destroy');
        }

        $emp.select2({
            placeholder: '-- Select Employee --',
            allowClear: true,
            width: '100%',
            minimumInputLength: 0,   // show list immediately on open
            ajax: {
                //url: '@Url.Action("GetEmployeeByCodePaged", "LeaveApplicationEntry")',
                url: '/LeaveApplicationEntry/GetEmployeeByCodePaged',
                type: 'GET',
                dataType: 'json',
                delay: 300,           // debounce typing
                data: function (params) {
                    return {
                        compCode: companyCode,
                        search: params.term || '',
                        page: params.page || 1,
                        pageSize: 20
                    };
                },
                processResults: function (response, params) {
                    params.page = params.page || 1;
                    var items = [];
                    if (response.success && response.data.results) {
                        items = response.data.results.map(function (emp) {
                            return {
                                id: emp.employeeId,
                                text: emp.employeeId + ' - ' + emp.firstName + ' ' + (emp.lastName || '')
                            };
                        });
                    }
                    return {
                        results: items,
                        pagination: { more: response.data.hasMore }
                    };
                },
                cache: true
            },
            language: {
                inputTooShort: function () { return ''; },
                noResults: function () { return 'No employees found'; },
                searching: function () { return 'Searching...'; },
                loadingMore: function () { return 'Loading more employees...'; }
            }
        });
    }

    // Company change → re-initialize employee Select2
    $('#companyDropdown').on('change', function () {
        var companyCode = $(this).val();
        $('#employeeDropdown').val(null).trigger('change'); // reset

        if (companyCode) {
            initEmployeeSelect2(companyCode);
        } else {
            // destroy select2 and reset to plain placeholder
            var $emp = $('#employeeDropdown');
            if ($emp.data('select2')) $emp.select2('destroy');
            $emp.empty().append('<option value="">-- Select Employee --</option>');
        }
    });

    // Remove the old getEmpByComp function entirely — it's replaced above.


    //function getEmpByComp(companyCode) {
    //    $.ajax({
    //        url: '@Url.Action("GetEmployeeByCode", "LeaveApplicationEntry")',
    //        type: 'GET',
    //        data: { compCode: companyCode },
    //        success: function (response) {
    //            console.log('GetEmployeeByCode---- : ', response)
    //            $('#employeeDropdown').empty().append('<option value="">--Select Employee--</option>');
    //            if (response.data && response.data.length > 0) {
    //                response.data.forEach(function (employee) {
    //                    $('#employeeDropdown').append(`<option value="${employee.employeeId}">${employee.employeeId} - ${employee.firstName}</option>`);
    //                });
    //            }
    //        },
    //        error: function () {
    //            toastr.warning('Failed to load employees.', 'Message');

    //            //alert('Failed to load employees.');
    //        }
    //    });
    //}

    //-----//---Search Employee---------------

    //             // Add Select2 initialization after employee dropdown population
    // $('#companyDropdown').on('change', function () {
    //     var companyCode = $(this).val();
    //     if (companyCode) {
    //         $.ajax({
    //             url: '/LeaveApplicationEntry/GetEmployeeByCode',
    //             type: 'GET',
    //             data: { compCode: companyCode },
    //             success: function (response) {
    //                 console.log('GetEmployeeByCode : ', response)
    //                 // Clear and initialize the dropdown
    //                 var $employeeDropdown = $('#employeeDropdown');
    //                 $employeeDropdown.empty().append('<option value="">--Select Employee--</option>');

    //                 if (response.data.result && response.data.result.length > 0) {
    //                     response.data.result.forEach(function (employee) {
    //                         $employeeDropdown.append(`<option value="${employee.employeeId}">${employee.employeeId} - ${employee.firstName}</option>`);
    //                     });
    //                 }

    //                 // Initialize Select2 with custom options
    //                 $employeeDropdown.select2({
    //                     placeholder: 'Search employee...',
    //                     allowClear: true,
    //                     width: '100%',
    //                     minimumInputLength: 1,
    //                     templateResult: formatEmployee,
    //                     templateSelection: formatEmployeeSelection
    //                 });
    //             },
    //             error: function () {
    //                 toastr.warning('Failed to load employees.', 'Message');
    //             }
    //         });
    //     } else {
    //         var $employeeDropdown = $('#employeeDropdown');
    //         $employeeDropdown.empty().append('<option value="">--Select Employee--</option>');
    //         $employeeDropdown.select2('destroy');
    //         $employeeDropdown.select2({
    //             placeholder: 'Search employee...',
    //             allowClear: true,
    //             width: '100%'
    //         });
    //     }
    // });

    // // Custom formatting for dropdown options
    // function formatEmployee(employee) {
    //     if (!employee.id) return employee.text;

    //     // Split the text to get ID and name separately
    //     var parts = employee.text.split(' - ');
    //     var empId = parts[0];
    //     var name = parts[1];

    //     return $(`<div>
    //         <strong>${empId}</strong><br>
    //         <small>${name}</small>
    //     </div>`);
    // }

    // // Custom formatting for selected option
    // function formatEmployeeSelection(employee) {
    //     if (!employee.id) return employee.text;
    //     return employee.text; // Keep the original format for selected value
    // }



    ///-------------///-------------


    //------////------------------








    // Employee Dropdown Change Event


    $('#employeeDropdown').on('change', function () {
        var selectedEmpId = $(this).val();
        if (selectedEmpId) {
            $.ajax({
                url: '/LeaveApplicationEntry/GetEmployeeOfficialByEmpId',
                type: 'GET',
                data: { EmpId: selectedEmpId },
                success: function (response) {
                    console.log('/LeaveApplicationEntry/GetEmployeeOfficialByEmpId', response)
                    if (response.success) {
                        // Populate employee details
                        $('#employeeName').val(response.data.result.employeeFirstName + ' ' + response.data.result.employeeLastName);
                        $('#designation').val(response.data.result.designationName);
                        $('#department').val(response.data.result.departmentName);

                        var rest = response.data.result;
                        console.log('/LeaveApplicationEntry/GetEmployeeOfficialByEmpId Rest : ', rest)
                        // Handle supervisor details
                        var supervisorOption = rest.reportingFirstName
                            ? `<option value="${rest.reportingTo}">${rest.reportingFirstName} ${rest.reportingLastName || ''}</option>`
                            : '<option value="">--No Supervisor--</option>';

                        // Handle HOD details
                        var hodOption = `<option value="${rest.hod}">${rest.hodFirstName} ${rest.hodLastName || ''}</option>`;

                        // Update dropdowns
                        $('#immediateSupervisor').empty().append(supervisorOption);
                        $('#headOfDepartment').empty().append(hodOption);
                    } else {
                        toastr.warning('Failed to fetch employee details.', 'Message');

                        // alert('Failed to fetch employee details.');
                    }
                },
                error: function () {
                    toastr.warning('Error fetching employee details.', 'Message');

                    // alert('Error fetching employee details.');
                }
            });
        } else {
            // Clear fields if no employee selected
            clearEmployeeFields();
        }
    });

    // Function to clear employee fields
    function clearEmployeeFields() {
        $('#employeeName').val('');
        $('#designation').val('');
        $('#department').val('');
        $('#immediateSupervisor').empty().append('<option value="">--Select Supervisor--</option>');
        $('#headOfDepartment').empty().append('<option value="">--Select HOD--</option>');
    }

    // Leave Format Change Event
    $("#applyLeaveFormat").change(function () {
        const selectedFormat = $(this).val();

        // Hide all sections first
        $("#fullDayInputs, #halfDayInputs, #oneDayInputs").hide();  /*  , #calendarContainer */

        // Show relevant section based on selection
        switch (selectedFormat) {
            case "fullDayLeave":
                $("#fullDayInputs, #calendarContainer").show();
                calendar.render();
                setTimeout(() => calendar.updateSize(), 100);
                break;
            case "halfDayLeave":
                $("#halfDayInputs").show();
                break;
            case "shortLeave":
                $("#oneDayInputs").show();
                break;
        }
    });

    // Full Day Leave Date Calculations
    $("#fromDate, #toDate").change(function () {
        const fromDate = new Date($("#fromDate").val());
        const toDate = new Date($("#toDate").val());

        if (fromDate && toDate && !isNaN(fromDate) && !isNaN(toDate)) {
            updateDateRange(fromDate, toDate);
        }
    });

    // One Day Leave Time Calculations
    $("#fromTime, #toTime").change(function () {
        const fromTime = $("#fromTime").val();
        const toTime = $("#toTime").val();

        if (fromTime && toTime) {
            const from = new Date(`1970-01-01T${fromTime}`);
            const to = new Date(`1970-01-01T${toTime}`);
            const difference = (to - from) / (1000 * 60 * 60);
            $("#hoursBetween").val(difference >= 0 ? difference.toFixed(2) : "0");
        }
    });

    // Half Day Leave Controls
    $("#halfDayCheckbox").change(function () {
        if ($(this).is(":checked")) {
            $("input[name='halfDayType']").prop('disabled', false);
        } else {
            $("input[name='halfDayType']").prop('disabled', true).prop('checked', false);
        }
    });

    // Calendar Setup
    // Calendar Setup
    let selectedDates = [];
    var calendarEl = document.getElementById('calendar');

    const calendar = new FullCalendar.Calendar(calendarEl, {
        height: 435,
        initialView: 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth'
        },
        selectable: true,
        unselectAuto: false,
        select: function (info) {
            const date = info.startStr;
            // First, check if we already have this date
            const existingEvent = calendar.getEvents().find(event =>
                event.startStr.split('T')[0] === date
            );

            if (existingEvent) {
                // If date exists, remove it
                existingEvent.remove();
                selectedDates = selectedDates.filter(d => d !== date);
            } else {
                // If date doesn't exist, add it
                selectedDates.push(date);
                calendar.addEvent({
                    id: date, // Add an ID to make it easier to find events
                    title: 'Selected',
                    start: date,
                    allDay: true,
                    backgroundColor: '#28a745',
                    borderColor: '#28a745'
                });
            }

            calendar.unselect();
            updateDayCount();
            updateDateInputs();
        },
        eventClick: function (info) {
            const date = info.event.startStr.split('T')[0];
            selectedDates = selectedDates.filter(d => d !== date);
            info.event.remove();
            updateDayCount();
            updateDateInputs();
        },
        // Ensure consistent date formatting
        eventDidMount: function (info) {
            if (!info.event.id) {
                info.event.setExtendedProp('id', info.event.startStr.split('T')[0]);
            }
        }
    });

    // Updated helper function for populating calendar
    function clearAndPopulateCalendar(dates) {
        // Clear existing events
        calendar.removeAllEvents();
        selectedDates = [];

        // Add new dates
        dates.forEach(date => {
            const formattedDate = date.split('T')[0];
            if (!selectedDates.includes(formattedDate)) {
                selectedDates.push(formattedDate);
                calendar.addEvent({
                    id: formattedDate,
                    title: 'Selected',
                    start: formattedDate,
                    allDay: true,
                    backgroundColor: '#28a745',
                    borderColor: '#28a745'
                });
            }
        });

        updateDayCount();
        updateDateInputs();
    }

    // Use this function when populating form for edit
    function populateCalendarDates(data) {
        if (data.days && Array.isArray(data.days)) {
            clearAndPopulateCalendar(data.days);
        }
    }


    // Add CSS
    $('#calendar').css({
        'max-width': '100%',


        'font-size': '0.9em'
    });

    // Optional: Make day cells smaller
    $('#calendar').find('.fc-daygrid-day').css({
        'padding': '1px'
    });


    //-----------------------------

    // Updated Calendar Helper Functions
    function updateDateInputs() {
        if (selectedDates.length > 0) {
            selectedDates.sort();
            $("#fromDate").val(selectedDates[0]);
            $("#toDate").val(selectedDates[selectedDates.length - 1]);
            $("#daysBetween").val(selectedDates.length);      // $("#daysBetween").val(calculateBusinessDays());

            updateDayCount(); // Call updateDayCount when dates are updated
        } else {
            $("#fromDate").val('');
            $("#toDate").val('');
            $("#daysBetween").val('0');
        }
    }

    function calculateBusinessDays() {
        // if (selectedDates.length === 0) return 0;

        // let count = 0;
        // // Count only the specifically selected dates that are business days
        // selectedDates.forEach(dateStr => {
        //     const date = new Date(dateStr);
        //     const dayOfWeek = date.getDay();
        //     if (dayOfWeek !== 0 && dayOfWeek !== 6) {
        //         count++;
        //     }
        // });
        // return count;

        return selectedDates.length;
    }

    function updateDayCount() {
        const businessDays = calculateBusinessDays();
        $("#daysBetween").val(businessDays);
    }

    function updateDateRange(fromDate, toDate) {
        // Clear existing selections
        selectedDates = [];
        calendar.getEvents().forEach(event => event.remove());

        if (fromDate <= toDate) {
            let currentDate = new Date(fromDate);
            while (currentDate <= toDate) {
                const dateStr = currentDate.toISOString().split('T')[0];

                // // Only add business days
                // const dayOfWeek = currentDate.getDay();
                // if (dayOfWeek !== 0 && dayOfWeek !== 6) {

                selectedDates.push(dateStr);
                calendar.addEvent({
                    title: 'Selected',
                    start: dateStr,
                    allDay: true,
                    backgroundColor: '#28a745',
                    borderColor: '#28a745'
                });
                //  }
                currentDate.setDate(currentDate.getDate() + 1);
            }
            updateDayCount();
        }
    }

    // Update the calendar select event handler
    calendar.setOption('select', function (info) {
        const date = info.startStr;


        // const selectedDate = new Date(date);
        //  const dayOfWeek = selectedDate.getDay();



        //-------//-----------//--------------//-----------//--------
        // Only allow selection of business days
        // if (dayOfWeek !== 0 && dayOfWeek !== 6) {
        if (!selectedDates.includes(date)) {
            selectedDates.push(date);
            calendar.addEvent({
                title: 'Selected',
                start: date,
                allDay: true,
                backgroundColor: '#28a745',
                borderColor: '#28a745'
            });
        }
        updateDayCount();
        updateDateInputs();
        // } else {
        //     alert('Please select only business days (Monday to Friday).');
        // }
    });

    //------------------------------------

    // Form Submission
    // Replace the existing form submission with this:
    $("form").on("submit", function (e) {
        e.preventDefault();

        var formData = new FormData();

        // Add file
        var fileInput = $("#leaveFile")[0].files[0];
        if (fileInput) {
            formData.append("LeaveFile", fileInput);
        }

        // Add other form fields
        formData.append("EntryId", $("#entryId").val());
        formData.append("LeaveAppEntryCode", $("#leaveAppEntryCode").val());
        formData.append("CompanyCode", $("#companyDropdown").val());
        formData.append("EmployeeId", $("#employeeDropdown").val());
        formData.append("ImmediateSupervisor", $("#immediateSupervisor").val());
        formData.append("HeadOfDepartment", $("#headOfDepartment").val());
        formData.append("LeaveFormat", $("#applyLeaveFormat").val());
        formData.append("LeaveType", $("#leaveTypeDropdown").val());
        formData.append("Reason", $("textarea").val());

        // Format-specific data
        const leaveFormat = $("#applyLeaveFormat").val();
        switch (leaveFormat) {
            case "fullDayLeave":
                formData.append("FromDate", $("#fromDate").val());
                formData.append("ToDate", $("#toDate").val());
                formData.append("TotalDays", $("#daysBetween").val());

                selectedDates.forEach((date, index) => {
                    formData.append(`SelectedDates[${index}]`, date);
                });


                // formData.append("SelectedDates", JSON.stringify(selectedDates));
                break;

            case "halfDayLeave":
                formData.append("IsHalfDay", $("#halfDayCheckbox").is(":checked"));
                formData.append("IsFirstHalf", $("#firstHalf").is(":checked"));
                formData.append("IsSecondHalf", $("#secondHalf").is(":checked"));
                formData.append("HalfDate", $("#halfDate").val());
                break;

            case "shortLeave":
                formData.append("FromTime", $("#fromTime").val());
                formData.append("ToTime", $("#toTime").val());
                formData.append("TotalHours", $("#hoursBetween").val());
                break;
        }
        console.log('formData submit - ', formData)

        $.ajax({

            url: '/LeaveApplicationEntry/SubmitLeaveApplication',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {


                if (response.isSuccess) {
                    // alert('  Leave application submitted successfully! ');
                    toastr.success('  Leave application submitted successfully! ', 'Success');
                    resetForm();
                    reloadDataTable();


                } else {
                    toastr.warning(response.message, 'Message');
                    //  alert(' Failed to submit: ' + response.message);

                    //window.location.href = '/LeaveApplicationEntry/Create';
                    console.log(response)
                }
            },
            error: function (xhr, status, error) {
                console.error('Error:', xhr.responseText);
                toastr.warning('Submission failed. Check console for details.', 'Message');
                //alert('Submission failed. Check console for details.');
            }
        });
    });







    // function resetForm() {
    //     $("form")[0].reset();
    //     selectedDates = [];
    //     calendar.getEvents().forEach(event => event.remove());
    //     $("#fullDayInputs, #halfDayInputs, #oneDayInputs").hide();   /* , #calendarContainer */
    //     clearEmployeeFields();
    //     fetchEntryId();
    // }


    function resetForm() {
        // Reset the form fields
        $("form")[0].reset();

        // Clear any selected dates in your calendar
        selectedDates = [];
        calendar.getEvents().forEach(event => event.remove());

        // Hide dynamic sections related to leave inputs
        $("#fullDayInputs, #halfDayInputs, #oneDayInputs").hide();

        // Clear any custom input fields or text
        clearEmployeeFields();

        // Clear the file input
        $('#leaveFile').val('');

        // Optionally reset select2 or other custom drop-downs
        $('#companyDropdown, #employeeDropdown, #immediateSupervisor, #headOfDepartment, #leaveTypeDropdown, #applyLeaveFormat').val('').trigger('change');

        $('#employeeDropdown').val('').trigger('change');

        // Reset any radio buttons or checkboxes
        $('input[type="radio"], input[type="checkbox"]').prop('checked', false);

        // Clear the reason text area
        $('textarea#Reason').val('');

        // Explicitly clear the date fields
        $('#fromDate').val('');
        $('#toDate').val('');
        $('#fromTime').val('');
        $('#toTime').val('');

        // Fetch a new entry ID (if this is a required part of the reset)
        fetchEntryId();
    }





    // Initialize calendar
    calendar.render();
});