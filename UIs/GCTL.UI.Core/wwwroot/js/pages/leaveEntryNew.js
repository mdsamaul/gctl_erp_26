
var gridState = {
    page: 1,
    pageSize: 10,
    search: '',
    sortColumn: '',
    sortDir: 'desc',
    empId : ''
};



$(document).ready(function () {


    getDateFromFlatPicker("#halfDate");
    getDateFromFlatPicker("#toDate");
    getDateFromFlatPicker("#fromDate");
    getDateFromFlatPicker("#shortDate");
    getDateFromFlatPicker("#halfDate");

    calculateDaysBetween();
    function calculateDaysBetween() {
        var fromDate = $("#fromDate").val();
        var toDate = $("#toDate").val();
        
      



        if (fromDate && toDate) {
            var start = new Date(fromDate);
            var end = new Date(toDate);

            // Difference in milliseconds
            var diff = end - start ;

            // Convert to days
            var days = diff / (1000 * 60 * 60 * 24);

            // Ensure non-negative
            if (days < 0) {
                days = 0;
            }

            $("#daysBetween").val(days);
        } else {
            $("#daysBetween").val('0');
        }
    }

    $("#test").on("click", function () {
        alert("Leave Apply button clicked!");

        var toTimeHidden = $("#toTimeHidden").val();
        var fromTimeHidden = $("#fromTimeHidden").val();
        var toTime = $("#toTime").val();
        var fromTime = $("#fromTime").val();
    });
    
    function getDateFromFlatPicker(id) {
       
        flatpickr(id, CalendarService.createConfig(
            {
                defaultDate: new Date(),
            }
        ));
    }



    initTimePicker('fromTime', new Date(), false);
    initTimePicker('toTime', new Date(), false);

    function initTimePicker(inputId, defaultTime, disableTime) {
        const input = document.getElementById(inputId);
        const hiddenInput = document.getElementById(inputId + 'Hidden');
        if (!input) return;

        if (input._flatpickr) {
            try { input._flatpickr.destroy(); } catch (e) { }
            input._flatpickr = undefined;
        }

        const fp = flatpickr(input, {
            enableTime: true,
            noCalendar: true,
            dateFormat: "h:i:s K",
            time_24hr: false,
            enableSeconds: true,
            inline: true,
            defaultDate: defaultTime || new Date(),
            minuteIncrement: 1,
            onChange: function (selectedDates) {
                if (selectedDates.length > 0 && hiddenInput) {
                    hiddenInput.value = selectedDates[0].toLocaleTimeString('en-US', {
                        hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true
                    });
                }
            }
        });

        if (disableTime) {
            input.disabled = true;
            input.readOnly = true;
            if (fp.calendarContainer) fp.calendarContainer.classList.add('fp-ui-disabled');
        } else {
            input.disabled = false;
            input.readOnly = false;
            if (fp.calendarContainer) fp.calendarContainer.classList.remove('fp-ui-disabled');
        }
    }


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

    initCompanySelected();
    function initCompanySelected() {
        var $dropdown = $("#companyDropdown");
        if ($dropdown.find("option").length === 2) {
            // দ্বিতীয় option select করো
            $dropdown.prop("selectedIndex", 1).trigger("change");

            var companyCode = $("#companyDropdown").val();
            if (companyCode) {
                initEmployeeSelect2(companyCode);
            }
        }
    }

    

    //$dropdown.on("change", function () {
    //
    //});







   

    $('#employeeDropdown').on('select2:clear', function (e) {
        gridState.empId = '';
        clearEmployeeFields();
        loadGrid();
    });



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
           
            gridState.empId = selectedEmpId;
            loadGrid();


        } else {
            // Clear fields if no employee selected
            clearEmployeeFields();
            loadGrid();
        }
    });



    //#region Leave Taken table

    // ── Leave Taken table state ─────────────────────────────────────────
    var leaveTakenState = {
        allData: [],
        page: 1,
        pageSize: 10,
        search: '',
        sortColumn: '',
        sortDir: 'asc'
    };

    function EmployeeLeaveTaken(data) {
        leaveTakenState.allData = data || [];
        leaveTakenState.page = 1;
        leaveTakenState.search = '';
        $('#leaveTakenSearch').val('');
        $('#leaveTakenTable thead th[data-col]').removeClass('sort-asc sort-desc');
        renderLeaveTakenTable();
    }

    function getFilteredSortedLeaveTaken() {
        var rows = leaveTakenState.allData.slice();

        // Search
        if (leaveTakenState.search) {
            var term = leaveTakenState.search.toLowerCase();
            rows = rows.filter(function (r) {
                var startStr = r.startDate ? new Date(r.startDate).toLocaleDateString('en-GB') : '';
                var endStr = r.endDate ? new Date(r.endDate).toLocaleDateString('en-GB') : '';
                return (String(r.leaveAppEntryId || '').toLowerCase().includes(term)) ||
                    (startStr.toLowerCase().includes(term)) ||
                    (endStr.toLowerCase().includes(term)) ||
                    (String(r.noOfDay || '').toLowerCase().includes(term)) ||
                    (String(r.reason || '').toLowerCase().includes(term));
            });
        }

        // Sort
        if (leaveTakenState.sortColumn) {
            var col = leaveTakenState.sortColumn;
            var dir = leaveTakenState.sortDir === 'asc' ? 1 : -1;
            rows.sort(function (a, b) {
                var va = a[col], vb = b[col];
                if (col === 'startDate' || col === 'endDate') {
                    va = va ? new Date(va).getTime() : 0;
                    vb = vb ? new Date(vb).getTime() : 0;
                } else if (col === 'noOfDay') {
                    va = parseFloat(va) || 0;
                    vb = parseFloat(vb) || 0;
                } else {
                    va = String(va || '').toLowerCase();
                    vb = String(vb || '').toLowerCase();
                }
                if (va < vb) return -1 * dir;
                if (va > vb) return 1 * dir;
                return 0;
            });
        }

        return rows;
    }

    function renderLeaveTakenTable() {
        var $tbody = $('#leaveTakenTbody');
        $tbody.empty();

        var filtered = getFilteredSortedLeaveTaken();
        var totalRecords = filtered.length;
        var totalPages = Math.max(1, Math.ceil(totalRecords / leaveTakenState.pageSize));
        if (leaveTakenState.page > totalPages) leaveTakenState.page = totalPages;

        var start = (leaveTakenState.page - 1) * leaveTakenState.pageSize;
        var pageRows = filtered.slice(start, start + leaveTakenState.pageSize);

        if (pageRows.length === 0) {
            $tbody.html('<tr><td colspan="6" class="text-center">No records found.</td></tr>');
        } else {
            $.each(pageRows, function (i, row) {
                var startStr = row.startDate ? new Date(row.startDate).toLocaleDateString('en-GB') : '';
                var endStr = row.endDate ? new Date(row.endDate).toLocaleDateString('en-GB') : '';

                var daysHtml = '';
                if (Array.isArray(row.days)) {
                    daysHtml = row.days.map(d => new Date(d).toLocaleDateString('en-GB')).join(', ');
                } else if (row.days) {
                    daysHtml = new Date(row.days).toLocaleDateString('en-GB');
                }

                var tr = '<tr>' +
                    '<td style="color: blue;">' + (row.leaveAppEntryId || '') + '</td>' +
                    '<td>' + startStr + '</td>' +
                    '<td>' + endStr + '</td>' +
                    '<td>' + daysHtml + '</td>' +
                    '<td>' + (row.noOfDay || 0) + '</td>' +
                    '<td class="resn-cell">' + (row.reason || '') + '</td>' +
                    '</tr>';
                $tbody.append(tr);
            });
        }

        // Footer total (sum of ALL filtered rows, not just current page)
        var totalDays = filtered.reduce((sum, r) => sum + (parseFloat(r.noOfDay) || 0), 0);
        $('#leaveTakenTotalFooter').text(totalDays);

        // Info text
        var from = totalRecords === 0 ? 0 : start + 1;
        var to = Math.min(start + leaveTakenState.pageSize, totalRecords);
        // $('#leaveTakenInfo').text('Showing ' + from + ' to ' + to + ' of ' + totalRecords + ' results');
        $('#leaveTakenSummary').text('Showing ' + from + ' to ' + to + ' of ' + totalRecords + ' results');


        renderLeaveTakenPagination(totalRecords, totalPages, leaveTakenState.page);
    }

    function renderLeaveTakenPagination(totalRecords, totalPages, currentPage) {
        var $wrap = $('#leaveTakenPaginationWrap');
        $wrap.empty();

        if (totalRecords === 0) return;

      

        var ul = $('<ul class="pagination pagination-sm mb-0"></ul>');
        ul.append('<li class="page-item' + (currentPage === 1 ? ' disabled' : '') + '">' +
            '<a class="page-link leave-taken-page-btn" data-page="' + (currentPage - 1) + '" href="#">&laquo;</a></li>');

        var startPage = Math.max(1, currentPage - 2);
        var endPage = Math.min(totalPages, currentPage + 2);
        for (var p = startPage; p <= endPage; p++) {
            ul.append('<li class="page-item' + (p === currentPage ? ' active' : '') + '">' +
                '<a class="page-link leave-taken-page-btn" data-page="' + p + '" href="#">' + p + '</a></li>');
        }

        ul.append('<li class="page-item' + (currentPage === totalPages ? ' disabled' : '') + '">' +
            '<a class="page-link leave-taken-page-btn" data-page="' + (currentPage + 1) + '" href="#">&raquo;</a></li>');

        $wrap.append(ul);
    }

    $(document).on('change', '#leaveTakenPageSize', function () {
        leaveTakenState.pageSize = parseInt($(this).val());
        leaveTakenState.page = 1;
        renderLeaveTakenTable();
    });


    // ── Leave Taken table event bindings ────────────────────────────────
    $(document).on('click', '.leave-taken-page-btn', function (e) {
        e.preventDefault();
        var p = parseInt($(this).data('page'));
        if (p >= 1) {
            leaveTakenState.page = p;
            renderLeaveTakenTable();
        }
    });

    $(document).on('change', '#leaveTakenPageSize', function () {
        leaveTakenState.pageSize = parseInt($(this).val());
        leaveTakenState.page = 1;
        renderLeaveTakenTable();
    });

    var leaveTakenSearchTimer;
    $(document).on('input', '#leaveTakenSearch', function () {
        clearTimeout(leaveTakenSearchTimer);
        var val = $(this).val();
        leaveTakenSearchTimer = setTimeout(function () {
            leaveTakenState.search = val;
            leaveTakenState.page = 1;
            renderLeaveTakenTable();
        }, 300);
    });

    $(document).on('click', '#leaveTakenTable thead th[data-col]', function () {
        var col = $(this).data('col');
        if (leaveTakenState.sortColumn === col) {
            leaveTakenState.sortDir = leaveTakenState.sortDir === 'asc' ? 'desc' : 'asc';
        } else {
            leaveTakenState.sortColumn = col;
            leaveTakenState.sortDir = 'asc';
        }
        leaveTakenState.page = 1;

        $('#leaveTakenTable thead th[data-col]').removeClass('sort-asc sort-desc');
        $(this).addClass('sort-' + leaveTakenState.sortDir);

        renderLeaveTakenTable();
    });


    //#endregion 


    



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
                        return data !== null && data !== undefined ? data : '';
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


    // Add this inside your existing script

    // Listen for changes on halfDate and shortDate inputs
    $("#halfDate, #shortDate").change(function () {
        const selectedDate = $(this).val();  // Get the selected date

        if (selectedDate) {
            const formattedDate = formatDate(selectedDate); // Format the date to YYYY-MM-DD

            // Add the selected date as an event in FullCalendar
            calendar.addEvent({
                title: 'Selected',  // Title for the event
                start: formattedDate,
                allDay: true,
                backgroundColor: '#28a745', // Highlight color
                borderColor: '#28a745'
            });

            // Navigate to the month of the selected date
            calendar.gotoDate(formattedDate);

            // Re-render the calendar to show the selected event
            calendar.render();

            // Optional: Update any input fields or other date-related elements
            updateDateInputs();
        }
    });

   




    //----------------- Populate form with existing data
    function populateForm(data) {
        
        // Basic fields
        $('#leaveAppEntryCode').val(data.leaveAppEntryCode);
        $('#entryId').val(data.leaveAppEntryId);
        $('#companyDropdown').val(data.companyCode).trigger('change');

        

        setTimeout(() => {
            debugger
            var empId = data.employeeId;
            var empName = data.employeeId + ' - ' + (data.employeeName || '');

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

        $('#leaveTypeDropdown').val(data.leaveTypeId).trigger('change');
        $('textarea[name="Reason"]').val(data.reason);

        var appleva = '';


        // Format specific data population
        switch (data.applyLeaveFormat) {
            case 'FullLeave':
                if (data.startDate) {
                   // $('#fromDate').val(data.startDate.split('T')[0]);
                    $("#fromDate")[0]._flatpickr.setDate(formatDate(data.startDate));

                }
                if (data.endDate) {
                   // $('#toDate').val(data.endDate.split('T')[0]);
                    $("#toDate")[0]._flatpickr.setDate(formatDate(data.endDate));

                }
                $('#daysBetween').val(data.noOfDay);

                appleva = 'fullDayLeave';

                // Clear existing calendar events
                calendar.getEvents().forEach(event => event.remove());
                selectedDates = [];
                debugger
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

                // Ensure the calendar goes to the correct month based on the first selected date
                const firstSelectedDate = selectedDates[0]; // Use the first selected date for navigation
                calendar.gotoDate(firstSelectedDate);  // Navigate to the month of the first selected date

                // Re-render the calendar to ensure it displays the selected events
                calendar.render();

                // Optionally update the calendar size if required (usually for responsive layouts)
                calendar.updateSize();

                //setTimeout(() => {
                //    calendar.render();
                //    calendar.updateSize();
                //}, 100);

                
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


    // ── Manual server-side grid ──────────────────────────────────────────────
   

    function initializeDataTable() {
        loadGrid();
    }

    function reloadDataTable() {
        gridState.page = 1;
        loadGrid();
    }

    function loadGrid() {
        
        var $tbody = $('#leaveApplicationsTable tbody');
        $tbody.html('<tr><td colspan="14" class="text-center"><i class="fas fa-spinner fa-spin"></i> Loading...</td></tr>');

       

        $.ajax({
            url: '/LeaveApplicationEntry/Grid',
            type: 'GET',
            data: {
                page: gridState.page,
                pageSize: gridState.pageSize,
                searchValue: gridState.search,
                sortColumn: gridState.sortColumn,
                sortDir: gridState.sortDir,
                empId: gridState.empId
            },
            success: function (response) {
                renderGridRows(response.data);
                renderPagination(response.totalRecords, response.totalPages, response.currentPage, response.pageSize);
            },
            error: function () {
                $tbody.html('<tr><td colspan="14" class="text-center text-danger">Failed to load data.</td></tr>');
            }
        });
    }

    function renderGridRows(data) {
        var $tbody = $('#leaveApplicationsTable tbody');
        $tbody.empty();

        if (!data || data.length === 0) {
            $tbody.html('<tr><td colspan="14" class="text-center">No leave applications found.</td></tr>');
            return;
        }

        $.each(data, function (i, row) {
            var actionBtns =
                '<button onclick="editLeaveApplication(\'' + row.leaveAppEntryCode + '\')" class="btn btn-primary btn-sm mb-1"><i class="fas fa-edit"></i></button> ' +
                '<button onclick="deleteLeaveApplication(\'' + row.leaveAppEntryCode + '\')" class="btn btn-danger btn-sm"><i class="fas fa-trash"></i></button>';

            var fileBtnHtml = (!row.sickLeaveFilePath)
                ? '<span>No file</span>'
                : '<button onclick="downloadLeaveApplication(\'' + row.sickLeaveFilePath + '\')" class="btn btn-info btn-sm"><i class="fas fa-download"></i></button>';

            var startDate = row.startDate ? new Date(row.startDate).toLocaleDateString() : '';
            var endDate = row.endDate ? new Date(row.endDate).toLocaleDateString() : '';

            var daysHtml = '';
            //if (Array.isArray(row.days) && row.days.length > 0) {
            //    daysHtml = '<ul style="list-style-type:none;margin:0;padding:0;">' +
            //        row.days.map(function (d) { return '<li>' + d.split('T')[0] + '</li>'; }).join(', ') +
            //        '</ul>';
            //}

           
            if (Array.isArray(row.days) && row.days.length > 0) {
                daysHtml = '<ul style="list-style-type:none;margin:0;padding:0;">' +
                    row.days.map(function (d) {
                        return '<li>' + d.split('T')[0] + ',' + '</li>';
                    }).join('') +
                    '</ul>';
            }

           
            //if (Array.isArray(row.days) && row.days.length > 0) {
            //    daysHtml = row.days
            //        .map(function (d) { return d.split('T')[0]; })
            //        .join(', ');
            //}


            var approvalHtml = formatApprovalStatus(row.hodApprovalStatus);

            var tr = '<tr>' +
                '<td class="text-center" style="width: 5%;">' + actionBtns + '</td>' +
                '<td class="text-center">' + (row.leaveAppEntryId || '') + '</td>' +
                '<td class="text-center">' + (row.applyLeaveFormat || '') + '</td>' +
                '<td class="text-center">' + approvalHtml + '</td>' +
                '<td class="text-center">' + (row.employeeID || '') + '</td>' +
                '<td class="text-start">' + (row.employeeFirstName || '') + '</td>' +
                '<td class="text-center">' + (row.leaveTypeId || '') + '</td>' +
                '<td class="text-center">' + fileBtnHtml + '</td>' +
                '<td class="text-center">' + startDate + '</td>' +
                '<td class="text-center">' + endDate + '</td>' +
                '<td class="text-center">' + daysHtml + '</td>' +
                '<td class="text-center">' + (row.noOfDay || '') + '</td>' +
                '<td class="text-start">' + (row.reason || '') + '</td>' +
                //'<td><input type="checkbox" class="leaveSelectChk" value="' + (row.leaveAppEntryId || '') + '"></td>' +
                '</tr>';
            $tbody.append(tr);
        });
    }

    function renderPagination(totalRecords, totalPages, currentPage, pageSize) {
        var $wrap = $('#gridPaginationWrap');
        var $showres = $('#showres');
        $wrap.empty();
        $showres.empty();

        if (totalPages <= 0) return;

        // Info text
        var from = (currentPage - 1) * pageSize + 1;
        var to = Math.min(currentPage * pageSize, totalRecords);
        $wrap.append('<span class="me-3 text-muted small">Showing ' + from + '–' + to + ' of ' + totalRecords + '</span>');

        // Page-size selector
        var pageSizeHtml = '<select id="gridPageSize" class="form-control form-control-sm d-inline-block me-3" style="width:auto;">';
        [10, 25, 50, 100].forEach(function (n) {
            pageSizeHtml += '<option value="' + n + '"' + (n === pageSize ? ' selected' : '') + '>' + n + '</option>';
        });
        pageSizeHtml += '</select>';
        $showres.append(pageSizeHtml);

        // Prev / page numbers / Next
        var ul = $('<ul class="pagination pagination-sm mb-0 d-inline-flex"></ul>');

        ul.append('<li class="page-item' + (currentPage === 1 ? ' disabled' : '') + '">' +
            '<a class="page-link grid-page-btn" data-page="' + (currentPage - 1) + '" href="#">&laquo;</a></li>');

        var startPage = Math.max(1, currentPage - 2);
        var endPage = Math.min(totalPages, currentPage + 2);
        for (var p = startPage; p <= endPage; p++) {
            ul.append('<li class="page-item' + (p === currentPage ? ' active' : '') + '">' +
                '<a class="page-link grid-page-btn" data-page="' + p + '" href="#">' + p + '</a></li>');
        }

        ul.append('<li class="page-item' + (currentPage === totalPages ? ' disabled' : '') + '">' +
            '<a class="page-link grid-page-btn" data-page="' + (currentPage + 1) + '" href="#">&raquo;</a></li>');

        $wrap.append(ul);
    }

    // Pagination click
    $(document).on('click', '.grid-page-btn', function (e) {
        e.preventDefault();
        var p = parseInt($(this).data('page'));
        if (p >= 1) {
            gridState.page = p;
            loadGrid();
        }
    });

    // Page-size change
    $(document).on('change', '#gridPageSize', function () {
        gridState.pageSize = parseInt($(this).val());
        gridState.page = 1;
        loadGrid();
    });

    // Search
    var gridSearchTimer;
    $(document).on('input', '#gridSearchInput', function () {
        clearTimeout(gridSearchTimer);
        var val = $(this).val();
        gridSearchTimer = setTimeout(function () {
            gridState.search = val;
            gridState.page = 1;
            loadGrid();
        }, 400);
    });

    // Sort
    $(document).on('click', '#leaveApplicationsTable thead th[data-col]', function () {
        var col = $(this).data('col');
        if (gridState.sortColumn === col) {
            gridState.sortDir = (gridState.sortDir === 'asc') ? 'desc' : 'asc';
        } else {
            gridState.sortColumn = col;
            gridState.sortDir = 'asc';
        }
        gridState.page = 1;

        // Update sort icons
        $('#leaveApplicationsTable thead th[data-col]').removeClass('sort-asc sort-desc');
        $(this).addClass('sort-' + gridState.sortDir);

        loadGrid();
    });





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




    window.deleteLeaveApplication = function (id) {
        // Show the modal
        $('#confirmDeleteModal').modal('show');

        // Attach click handler for confirmation
        $('#confirmDeleteBtn').off('click').on('click', function () {
            console.log('Deleting leave application with ID:', id);

            $.ajax({
                url: `/LeaveApplicationEntry/DeleteLeaveApplication`,
                type: 'GET',
                data: { id: id },
                success: function (response) {
                    console.log('Response: delete', response);
                    toastr.success(response.message, 'Success');
                    reloadDataTable();
                    fetchEntryId();
                },
                error: function (xhr, status, error) {
                    console.error('Error:', error);
                    toastr.warning('Failed to delete leave application. Please try again.', 'Message');
                }
            });

            // Hide modal after action
            $('#confirmDeleteModal').modal('hide');
        });
    };




    // Define the function globally
    //window.deleteLeaveApplication = function (id) {
    //    // Show a confirmation dialog
    //    var confirmation = confirm('Are You sure to Delete this leave Application');

    //    // If the user clicks "OK", proceed with the deletion
    //    if (confirmation) {
    //        console.log('Deleting leave application with ID:', id);

    //        // Perform an AJAX GET request to fetch data or perform an action
    //        $.ajax({
    //            url: `/LeaveApplicationEntry/DeleteLeaveApplication`,
    //            type: 'GET',
    //            data: { id: id },
    //            success: function (response) {
    //                // Handle the successful response
    //                console.log('Response: delete', response);
    //                toastr.success(response.message, 'Success');
    //                // alert('Leave application deleted successfully.');
    //                //window.location.href = `/LeaveApplicationEntry/Create`
    //                reloadDataTable();
    //                fetchEntryId();
    //            },
    //            error: function (xhr, status, error) {
    //                // Handle the error response
    //                console.error('Error:', error);
    //                // alert('Failed to delete leave application. Please try again.');
    //                toastr.warning('Failed to delete leave application. Please try again.', 'Message');
    //            }
    //        });
    //    } else {
    //        // User clicked "Cancel", do nothing
    //        console.log('Deletion canceled by user.');
    //    }
    //};







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
    $(document).on('change', "#fromTime, #toTime", function () {
     
        const fromTime = $("#fromTime").val();
        const toTime = $("#toTime").val();

       

        

        if (fromTime && toTime) {
            const from = parseTime(fromTime);
            let to = parseTime(toTime);

            if (to < from) {
                to.setDate(to.getDate() + 1); // handle wrap-around
            }

            const difference = (to - from) / (1000 * 60 * 60);
            $("#hoursBetween").val(difference.toFixed(2));
        }

    });

    function parseTime(timeStr) {
        const [time, modifier] = timeStr.split(" ");
        let [hours, minutes, seconds] = time.split(":").map(Number);

        if (modifier === "PM" && hours < 12) hours += 12;
        if (modifier === "AM" && hours === 12) hours = 0;

        const date = new Date(1970, 0, 1, hours, minutes, seconds);
        return date;
    }

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

        calendar.render();

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
    const formatDate = dateStr => dateStr ? dateStr.split("T")[0] : "";
    // Updated Calendar Helper Functions
    function updateDateInputs() {
        
        if (selectedDates.length > 0) {
            
            selectedDates.sort();
           
            $("#fromDate")[0]._flatpickr.setDate(formatDate(selectedDates[0]));
            $("#toDate")[0]._flatpickr.setDate(formatDate(selectedDates[selectedDates.length - 1]));
           // $("#fromDate").val(selectedDates[0]);
           // $("#toDate").val(selectedDates[selectedDates.length - 1]);
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
                formData.append("ShortDate", $("#shortDate").val());

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
                console.log(response)

                if (response.isSuccess) {
                   
                    toastr.success('  Leave application submitted successfully! ', 'Success');
                    resetForm();
                    reloadDataTable();
                    var dar = response.data
                    if (dar && dar.edit) {
                        var empId = dar.id;
                        var empName = dar.id + ' - ' + (dar.empName || '');

                        // Pre-inject the option so Select2 can select it without an extra fetch
                        var $emp = $('#employeeDropdown');
                        if ($emp.find('option[value="' + empId + '"]').length === 0) {
                            $emp.append(new Option(empName, empId, true, true));
                        }
                        $emp.val(empId).trigger('change');
                    }


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
        initCompanySelected();
    }





    // Initialize calendar
    calendar.render();
});