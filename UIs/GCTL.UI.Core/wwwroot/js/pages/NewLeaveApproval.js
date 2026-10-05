$(document).ready(function () {
    var preSelectedLeaveId = $('#preSelected').val();
    var leavePendingTable;

    // Handle pre-selected leave if exists
    if (preSelectedLeaveId && preSelectedLeaveId !== '') {
        $.ajax({
            url: '/LeaveApplicationEntry/GetLeaveIDByEntryId',
            type: 'POST',
            data: { id: preSelectedLeaveId },
            success: function (response) {
                if (response.data == 'NotValid') {
                    toastr.warning("Leave Application is not valid");
                } else {
                    getLeaveApplication(response.data.leaveAppEntryCode);
                    toastr.success("Leave Application is loaded");
                }
            },
            error: function (xhr, status, error) {
                console.error("Error loading leave:", error);
                toastr.error("Unable to load the leave application");
            }
        });
    }

    // Initialize DataTable
    var leavePendingTable;

    // Initialize DataTable
    function initializeDataTable() {
        if ($.fn.DataTable.isDataTable('#leavePendingTable')) {
            $('#leavePendingTable').DataTable().destroy();
        }

        leavePendingTable = $('#leavePendingTable').DataTable({
            ajax: {
                url: '/LeaveApplicationEntry/GetLeavePendingByIsID',
                type: 'GET',
                dataSrc: function (response) {
                    console.log('leave pending table : ', response)
                    if (response.success && Array.isArray(response.data)) {
                        return response.data;
                    }
                    return [];
                }
            },
            columns: [
                {
                    data: null,
                    render: function (data, type, row) {
                        return `
                        
                        <input type="checkbox" class="row-checkbox" data-id="${row.leaveAppEntryId}" />
                        `;
                    },
                    orderable: false,
                    className: 'select-checkbox',
                    width: "2%"
                },
                {
                    data: 'leaveAppEntryId',
                    render: function (data, type, row) {
                        return `<button style="text-decoration: underline; color: rebeccapurple;" class="btn leave-entry-btn" data-id="${row.leaveAppEntryCode}"> ${data} </button>`;
                    },
                    width: "1%"
                },

                {
                    data: 'leaveFormat',
                    defaultContent: '', // ensures no error if 'leaveFormat' is missing
                    render: function (data, type, row) {
                        if (!data) return 'N/A';

                        switch (data) {
                            case 'fullDayLeave':
                                return 'Full Leave';
                            case 'halfDayLeave':
                                return 'HalfDay Leave';
                            case 'shortLeave':
                                return 'Short Leave';
                            default:
                                return 'Unknown';
                        }
                    },
                    width: "1%"
                },

                
                //{
                //    data: 'leaveFormat',
                //    render: function (data) {
                //        // console.log(data)
                //        if (data == 'fullDayLeave') {
                //            return 'Full Leave'
                //        } else if (data == 'halfDayLeave') {
                //            return 'HalfDay Leave'
                //        } else if (data == 'shortLeave') {
                //            return 'Short Leave'
                //        }
                //    },
                //    width: "1%"
                //},
                {
                    data: 'employeeID',
                    width: "1%"
                },
                {
                    data: 'employeeFirstName',
                    width: "1%"
                },
                {
                    data: 'designationName',
                    width: "1%"
                },
                {
                    data: 'isName',
                    width: "1%"
                },
                {
                    data: 'hodName',
                    width: "1%"
                },
                {
                    data: 'leaveTypeId',
                    //data: 'leaveTypeName',
                    width: "1%"
                },
                {
                    data: 'fromDate',
                    render: function (data) {
                        return data ? new Date(data).toLocaleDateString('en-GB') : '';
                    },
                    width: "1%"
                },
                {
                    data: 'toDate',
                    render: function (data) {
                        return data ? new Date(data).toLocaleDateString('en-GB') : '';
                    },
                    width: "1%"
                },
                {
                    data: 'days',
                    render: function (data) {
                        if (!data) return '';
                        return Array.isArray(data)
                            ? data.map(date => new Date(date).toLocaleDateString('en-GB')).join(', ')
                            : '';
                    },
                    width: "1%"
                },
                {
                    data: 'reason',
                    width: "5%"
                },
                {
                    data: 'nullHelper',
                    render: function (data, type, row) {
                        if (type === 'display') {
                            return `
                                <select class="form-control form-control-sm approval-status" data-id="${row.leaveAppEntryId}">
                                    <option value="">Select Status</option>
                                    <option value="approved" ${data === 'approved' ? 'selected' : ''}>Approved</option>
                                    <option value="rejected" ${data === 'rejected' ? 'selected' : ''}>Cancel</option>
                                </select>`;
                        }
                        return data;
                    },
                    width: "5%"
                },
                {
                    data: 'nullHelper',
                    render: function (data, type, row) {
                        if (type === 'display') {
                            //return `<textarea class="form-control form-control-sm remarks" data-id="${row.leaveAppEntryId}" rows="1">${data || ''}</textarea>`;
                            return `<textarea class="form-control form-control-sm remarks" data-id="${row.leaveAppEntryId}" rows="1"></textarea>`;
                        }
                        return data;
                    },
                    width: "5%"
                },
                {
                    data: null,
                    render: function (data, type, row) {
                        return `
                            <button class="btn btn-primary btn-sm apply-action" data-id="${row.leaveAppEntryId}">
                                <i class="fas fa-check"></i> Apply
                            </button>`;
                    },
                    width: "1%"
                }
            ],
            processing: true,
            serverSide: false,
            language: {
                processing: "Loading leave applications...",
                zeroRecords: "No leave applications found",
                emptyTable: "No leave applications available"
            },
            order: [[1, 'desc']],
            responsive: true,
            autoWidth: false,
            initComplete: function () {
                // Add a "Select All" checkbox in the header
                $('#leavePendingTable thead tr th:first-child').html('<span>Select</span> <br/> <input type="checkbox" id="select-all" />');
            }
        });
    }


  



    // Handle "Select All" checkbox
    $('#leavePendingTable').on('change', '#select-all', function () {
        const isChecked = $(this).is(':checked');
        $('.row-checkbox').prop('checked', isChecked).trigger('change');
    });

    // Initialize the table
    initializeDataTable();

    // Handle Apply button click
    $('#leavePendingTable').on('click', '.apply-action', function () {
        const leaveId = $(this).data('id');
        const approvalStatus = $(`.approval-status[data-id="${leaveId}"]`).val();
        const remarks = $(`.remarks[data-id="${leaveId}"]`).val();

        if (!approvalStatus) {
            toastr.warning('Please select an approval status');
            return;
        }

        const payload = {
            LeaveAppEntryId: leaveId,
            ApprovalStatus: approvalStatus,
            ConfirmationRemark: remarks
        };

        $.ajax({
            url: '/LeaveApplicationEntry/SubmitLeaveApproval',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            success: function (response) {
                toastr.success('Leave application updated successfully');
                initializeDataTable(); // Refresh the table
            },
            error: function (xhr, status, error) {
                toastr.error('Error updating leave application');
                console.error('Error:', error);
            }
        });
    });

    // Handle bulk operations
    $('#submitSelected, #submitRejected').on('click', function () {
        const action = $(this).attr('id') === 'submitSelected' ? 'approved' : 'rejected';
        const selectedIds = $('.row-checkbox:checked').map(function () {
            return $(this).data('id');
        }).get();

        if (selectedIds.length === 0) {
            toastr.warning('Please select at least one leave application');
            return;
        }

        // Update all selected rows
        selectedIds.forEach(id => {
            $(`.approval-status[data-id="${id}"]`).val(action);
        });

        toastr.success(`Selected applications marked as ${action}`);
    });

    // Select all functionality
    $('#select-all').on('change', function () {
        $('.row-checkbox').prop('checked', $(this).prop('checked'));
    });

    // Clear button functionality
    $('#clearBtn').on('click', function () {
        $('.row-checkbox').prop('checked', false);
        $('#select-all').prop('checked', false);
        $('.approval-status').val('');
        $('.remarks').val('');
    });

    //-----------------------

    //// Handle clicking the row (excluding buttons, checkboxes, inputs)
    //$('#leavePendingTable tbody').on('click', 'tr', function (e) {
    //    // Ignore clicks on buttons, checkboxes, select, and inputs
    //    if ($(e.target).is('button, .row-checkbox, select, textarea, input')) {
    //        return;
    //    }

    //    // Get row data
    //    let rowData = leavePendingTable.row(this).data();

    //    if (rowData) {
    //        console.log('Clicked Row Data:', rowData); // Debugging
    //        getLeaveApplication(rowData.leaveAppEntryId); // Use your existing function
    //    }
    //});

    // Handle leave entry button click separately
    $('#leavePendingTable').on('click', '.leave-entry-btn', function (e) {
        e.stopPropagation(); // Prevent the row click event from triggering
        const id = $(this).data('id');
        getLeaveApplication(id);
    });


    //// Handle leave entry button click
    //$('#leavePendingTable').on('click', '.leave-entry-btn', function () {
    //    const id = $(this).data('id');
    //    getLeaveApplication(id);
    //});

    // Get Leave Application Details
    function getLeaveApplication(id) {
        $.ajax({
            url: '/LeaveApplicationEntry/GetLeaveApplication1',
            type: 'POST',
            data: { id },
            success: function (response) {
                populateForm(response.data);
                getEmployeeInfo(response.data.employeeId);
                getLeaveInfo(response.data.leaveTypeId, response.data.employeeId);

               $('#leaveApprovalModal').modal('show');
            },
            error: function (xhr, status, error) {
                console.error("Error getting leave application:", error);
                toastr.warning("An error occurred while getting the leave application.");
            }
        });
    }

    // Get Employee Information
    function getEmployeeInfo(EmpId) {
        $.ajax({
            url: '/LeaveApplicationEntry/GetEmployeeOfficialByEmpId',
            type: 'POST',
            data: { EmpId },
            success: function (response) {
                populateFormEmp(response.data);
            },
            error: function (xhr, status, error) {
                console.error("Error getting employee info:", error);
                toastr.warning("An error occurred while getting employee information.");
            }
        });
    }

    // Get Leave Type Information
    function getLeaveInfo(LeaveTypeId, EmpId) {
        $.ajax({
            url: '/LeaveApplicationEntry/GetLeaveTypeByLeaveTypeIdEmpId',
            type: 'POST',
            data: { LeaveTypeId, EmpId },
            success: function (response) {
                populateFormLeave(response.data);
            },
            error: function (xhr, status, error) {
                console.error("Error getting leave info:", error);
                toastr.warning("An error occurred while getting leave information.");
            }
        });
    }

    // Populate Leave Form
    function populateForm(data) {
        var empOption = `<option value="${data.employeeId}">${data.employeeId || ''}</option>`;
        $('#employeeDropdown').empty().append(empOption);
        $('#leaveStatus').val(data.isApproved);
        $('#leaveAppEntryId').val(data.leaveAppEntryId);

        // Handle different leave formats
        switch (data.applyLeaveFormat) {
            case 'fullDayLeave':
                $('#fullDayInputs').show();
                $('#oneDayInputs, #halfDayInputs').hide();
                if (data.startDate) $('#fromDate').val(data.startDate.split('T')[0]);
                if (data.endDate) $('#toDate').val(data.endDate.split('T')[0]);
                $('#daysBetween').val(data.noOfDay);

                if (data.days && data.days.length > 0) {
                    const calendarDates = data.days.map(day => {
                        const date = new Date(day);
                        return `${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}/${date.getFullYear()}`;
                    }).join(", ");
                    $("#days").val(calendarDates);
                }
                break;

            case 'halfDayLeave':
                $('#halfDayInputs').show();
                $('#fullDayInputs, #oneDayInputs').hide();
                $('#halfDayCheckbox').prop('checked', data.halfDay === "1");
                $('#firstHalf').prop('checked', data.firstOrSecondHalf === "1");
                $('#secondHalf').prop('checked', data.firstOrSecondHalf === "2");
                $("#days").val('');
                break;

            case 'shortLeave':
                $('#oneDayInputs').show();
                $('#fullDayInputs, #halfDayInputs').hide();
                if (data.shortLeaveFrom) {
                    $('#fromTime').val(data.shortLeaveFrom.split('T')[1].substring(0, 5));
                }
                if (data.shortLeaveTo) {
                    $('#toTime').val(data.shortLeaveTo.split('T')[1].substring(0, 5));
                }
                $("#days").val('');
                break;
        }

     /*   $('textarea').first().val(data.reason);*/
        $('#rsnTxt').first().val(data.reason);

        if (data.ldate) {
            $('#createAt').val(new Date(data.ldate).toISOString().split('T')[0]);
        }
        if (data.modifyDate) {
            $('#updateAt').val(new Date(data.modifyDate).toISOString().split('T')[0]);
        } else {
            $('#updateAt').val("Not updated");
        }
    }

    // Populate Employee Form
    function populateFormEmp(data1) {
        const data = data1.result;
        $('#employeeName').val(data.employeeFirstName + ' ' + data.employeeLastName);
        $('#designation').val(data.designationName);
        $('#department').val(data.departmentName);

        const supervisorOption = data.reportingFirstName
            ? `<option value="${data.reportingTo}">${data.reportingFirstName} ${data.reportingLastName || ''}</option>`
            : '<option value="">--No Supervisor--</option>';

        const hodOption = `<option value="${data.hod}">${data.hodFirstName} ${data.hodLastName || ''}</option>`;

        $('#immediateSupervisor').empty().append(supervisorOption);
        $('#headOfDepartment').empty().append(hodOption);
    }

    // Populate Leave Type Form
    function populateFormLeave(data) {
        const leaveOption = `<option value="${data.leaveTypeCode}">${data.leaveName || ''}</option>`;
        $('#leaveType').empty().append(leaveOption);

        const availableLeave = data.remainingLeave;
        const totalLeave = data.availableLeave;
        const usedLeave = data.totalLeaveTaken;

        $('#leaveStatus').html(`<b>YGL: ${totalLeave}, AL: ${usedLeave}, BL: ${availableLeave}</b>`);
    }

    // Handle form submission
    $('#leaveApprovalForm').submit(function (e) {
        e.preventDefault();

        const LeaveApprovalViewModel = {
            LeaveAppEntryId: $('#leaveAppEntryId').val(),
            ApprovalStatus: $('#approvalStatus').val(),
            ConfirmationRemark: $('#confirmRemark').val()
        };

        $.ajax({
            url: '/LeaveApplicationEntry/SubmitLeaveApproval',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(LeaveApprovalViewModel),
            success: function (response) {
                toastr.success("Action performed successfully.");
                initializeDataTable();
                resetLeaveApprovalForm();

                $('#leaveApprovalModal').modal('hide');
            },
            error: function (xhr, status, error) {
                console.error("Error performing action:", error);
                toastr.warning("An error occurred while performing the action.");
            }
        });
    });

    // Handle bulk approvals
    $('#submitSelected').on('click', function () {
        submitBulkAction('/LeaveApplicationEntry/SubmitSelectedLeaves', 'approve');
    });

    // Handle bulk rejections
    $('#submitRejected').on('click', function () {
        submitBulkAction('/LeaveApplicationEntry/SubmitRejectedLeaves', 'reject');
    });

    // Bulk action helper function
    function submitBulkAction(url, action) {
        const selectedIds = $('.row-checkbox:checked').map(function () {
            return $(this).data('id');
        }).get();

        const remarks = $('#bulkConfirmRemark').val()
        ///alert(remarks)

        if (selectedIds.length === 0) {
            toastr.warning("Please select at least one leave application.");
            return;
        }

        $.ajax({
            url: url,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ leaveIds: selectedIds, remarks: remarks }),
            success: function (response) {
                toastr.success(`Selected leave applications ${action}ed successfully.`);
                initializeDataTable();
                $('.row-checkbox, #select-all').prop('checked', false);
            },
            error: function (xhr, status, error) {
                console.error(`Error ${action}ing leaves:`, error);
                toastr.warning(`An error occurred while ${action}ing the leave applications.`);
            }
        });
    }

    // Clear form
    $('#clearBtn').on('click', resetLeaveApprovalForm);

    // Reset form helper function
    function resetLeaveApprovalForm() {
        $('#leaveApprovalForm')[0].reset();
        $('#fullDayInputs, #halfDayInputs, #oneDayInputs').hide();
        $('#leaveStatus').empty();
        $('#days').val('');
        $('#employeeName, #createAt, #updateAt').val('');
        $('#immediateSupervisor, #headOfDepartment, #leaveType').empty();
    }

    // Send approval email helper
    function sendApprovalEmail(leaveId) {
        $.ajax({
            url: '/LeaveApplicationEntry/SendApprovalEmail',
            type: 'POST',
            data: { leaveAppEntryId: leaveId },
            success: function (response) {
                if (response.success) {
                    toastr.success("Approval email sent successfully");
                } else {
                    toastr.error(response.message);
                }
            },
            error: function (xhr, status, error) {
                toastr.error("Error sending approval email");
            }
        });
    }
});