
(function ($)
{
    $.leaveType = function (options)
    {
        // Default options
        var settings = $.extend({
            baseUrl: "/",
            formSelector: "#leaveType-form",
            formContainer: ".js-leaveType-form-container",
            gridSelector: "#leaveType-grid",
            gridContainer: ".js-leaveType-grid-container",
            editSelector: ".js-leaveType-edit",
            saveSelector: ".js-leaveType-save",
            selectAllSelector: "#leaveType-check-all",
            deleteSelector: ".js-leaveType-delete-confirm",
            deleteModal: "#leaveType-delete-modal",
            finalDeleteSelector: ".js-leaveType-delete",
            clearSelector: ".js-leaveType-clear",
            topSelector: ".js-go",
            decimalSelector: ".js-leaveType-decimalplaces",
            maxDecimalPlace: 5,
            showNagativeFormat: false,
            availabilitySelector: ".js-leaveTyp-check-duplicateName",
            haseFile: false,
            quickAddSelector: ".js-quick-add",
            quickAddModal: "#quickAddModal",
            lastCodeSelector: '#lastCode',
            load: function () { }
        }, options);

      
        var baseControllerNameUrl = "/LeaveTypes";
        var testSave = baseControllerNameUrl + "/Setup";
        var nextCodeULR = baseControllerNameUrl + "/GenerateNextLeaveTypeCode";
        var loadTableURL = baseControllerNameUrl + "/GeLeaveTypesList"; 
        var deleteURL = baseControllerNameUrl + "/Delete";
        var getById = baseControllerNameUrl + "/Index";
        var duplicateCheckURL = baseControllerNameUrl + "/CheckAvailability";
        var selectedItems = [];

        $(() =>
        {
            initialize();
            $("body").on('click', '#exportExcel', function () {
               
                window.location.href = '/LeaveTypes/ExportToExcel';
            });
            // Save button click event
            $("body").on('click', settings.saveSelector, function ()
            {
                validation();
                $(settings.formSelector).submit();
            });

            // Form submission event
            $("body").on('submit', settings.formSelector, function (e)
            {
                e.preventDefault();
                var form = $(this)[0];
                var formData = new FormData(form);
                var actionUrl = $(this).attr('action');

                $.ajax({
                    type: 'POST',
                    url: actionUrl,
                    data: formData,
                    contentType: false,
                    processData: false,
                    success: function (result) {
                        if (result.isDuplicate) {
                            toastr.error(result.message);
                        } else if (result.isSuccess) {
                            $(settings.gridContainer).html(result.html);
                            initialize();
                            toastr.success(result.message);
                        } else {
                            $(settings.formSelector).html(result);
                        }
                    },
                    error: function () {
                        toastr.error('An error occurred while saving leave type data.');
                    }
                });
            });

            // Availability check
            $("body").on("keyup", settings.availabilitySelector, function ()
            {
                var self = $(this);
                let code = $(".js-leaveType-code").val();
                let name = self.val();

                $.ajax({
                    url: duplicateCheckURL,
                    method: "POST",
                    data: { code: code, name: name },
                    success: function (response)
                    {
                        console.log(response);
                        if (response.isSuccess)
                        {
                            toastr.error(response.message);
                        }
                    }
                });
            });

            // Edit leave type
            $("body").on('click', settings.editSelector, function ()
            {
                var id = $(this).data('id');
                $.get(getById, { id: id }, function (result)
                {
                    $(settings.formSelector).html($(result).find(settings.formSelector).html());
                    WEFDatePicker();
                    select2DD();
                    $(settings.saveSelector).html('<i class="fas fa-edit"></i> Update');
                    $(settings.formSelector).attr('action', testSave);
                }).fail(function () {
                    toastr.error('Error fetching leave type details. Please try again.');
                });
            });

            // Select all checkboxes
            $("body").on('click', settings.selectAllSelector, function () {
                $('.checkBox').prop('checked', $(this).prop('checked'));
            });

            // Delete confirmation
            $("body").on('click', settings.deleteSelector, function (e)
            {
                e.preventDefault();
                var selectedIds = [];

                $('.checkBox:checked').each(function ()
                {
                    selectedIds.push($(this).val());
                });

                if (selectedIds.length === 0)
                {
                    toastr.error('Please select at least one leave type to delete.');
                    return;
                }

                $(settings.deleteModal + ' ' + settings.finalDeleteSelector).data('ids', selectedIds);
                $(settings.deleteModal).modal('show');
            });

            // Final delete action
            $("body").on('click', settings.finalDeleteSelector, function (e) {
                e.preventDefault();
                var selectedIds = $(this).data('ids');

                $.ajax({
                    type: 'POST',
                    url: deleteURL,
                    contentType: 'application/json',
                    data: JSON.stringify(selectedIds),
                    success: function (result)
                    {
                        if (result.isSuccess)
                        {
                            
                            loadLeaveTypesList(); 
                            GenerateNextLeaveTypeCode(); 
                            toastr.success(result.message); 
                        } else
                        {
                            
                            toastr.error(result.message); 
                        }
                        $(settings.deleteModal).modal('hide');
                    },
                    error: function (xhr)
                    {
                        toastr.error('An error occurred while deleting the selected leave types.');
                        $(settings.deleteModal).modal('hide');
                    }
                });
            });

            //

            

        });


       

        // Initialization function
        function initialize()
        {

            WEFDatePicker();
            GenerateNextLeaveTypeCode();
            loadLeaveTypesList();
            LeavetypeResetForm();
            select2DD();
           
        }

       
        // Load leave types list
        function loadLeaveTypesList()
        {
            $.ajax({
                type: 'GET',
                url: loadTableURL,
                success: function (data)
                {
                    $(settings.gridContainer).html(data);
                    dataTableLeaveType();
                   
                },
                error: function ()
                {
                    toastr.error('An error occurred while loading the leave type list.');
                }
            });
        }

        // Data table initialization function
        function dataTableLeaveType()
        {

            $('#leaveType-grid').DataTable({
                responsive: true,
                pageLength: 10,
                destroy: true,
                lengthMenu: [10, 25, 50, 100],
            });


        }

      

       
        
        // WEF Date Picker
        function WEFDatePicker()
        {
            $("#WefDate").datepicker({
                dateFormat: 'dd/mm/yy',
                changeMonth: true,
                changeYear: true,
                yearRange: "-10:+0",
                beforeShow: function (input, inst) {
                    $(this).toggleClass("placeholder-shown", !this.value);
                },
                onClose: function (dateText, inst) {
                    $(this).toggleClass("placeholder-shown", !this.value);
                },
                onSelect: function (dateText) {
                    var formattedDate = $.datepicker.formatDate('mm/dd/yy', $(this).datepicker('getDate'));
                    $('#WefHidden').val(formattedDate);
                    $("#WefDate").valid();
                }
            });

            var initialDate = $("#WefDate").val();
            if (initialDate) {
                var formattedInitialDate = $.datepicker.formatDate('mm/dd/yy', $("#WefDate").datepicker('getDate'));
                $('#WefHidden').val(formattedInitialDate);
            }

            $(".datepicker").trigger("input");
        }

        // Generate next leave type code function
        function GenerateNextLeaveTypeCode()
        {
            $("#LeaveTypeCode").val('');
            $.ajax({
                type: 'GET',
                url: nextCodeULR,
                success: function (result)
                {
                    $('#LeaveTypeCode').val(result);
                },
                error: function ()
                {
                    toastr.error('An error occurred while generating the next code.');
                }
            });
        }

       

     

        // Leave type reset form function
        function LeavetypeResetForm()
        {
            $(settings.formSelector)[0].reset();
            $('#Id').val('');
            $('#LeaveTypeCode').val('');
            $('#Name').val('').trigger('focus');
            $('#ShortName').val('');
            $('#RulePolicy').val('');
            $('#NoOfDay').val('');
            $('#Ymwd').val('Year').trigger('change');
            $('#WefDate').val('');
            $('#For').val('');
            $('#LdateModifyHide').hide();
            $(settings.saveSelector).html('<i class="fas fa-save"></i> Save');
            $('.text-danger').text('');
            $(settings.formSelector).attr('action', testSave);
            GenerateNextLeaveTypeCode();
            
        }

        // Clear form on click
        $(document).on('click', settings.clearSelector, function ()
        {
            LeavetypeResetForm();
        });

        function select2DD() {

            $('#Ymwd').select2({
                language: {
                    noResults: function () {

                    }
                },
                escapeMarkup: function (markup)
                {
                    return markup;
                }
            });

        }
       
        // Validation function
        function validation()
        {
            var name = $("input[name='Name']").val();
            var wef = $("#WefDate").val();

            if (!name)
            {
                toastr.info('Leave type name is required');
                $('#Name').trigger('focus');
                return false;
            }

            if (!wef)
            {
                toastr.info('W.E.F is required');
                $("#WefDate").trigger('focus');
                return false;
            }
        }

    }
}(jQuery));







//
//original js 

//(function ($) {
//    $.leaveType = function (options) {
//        // Default options
//        var settings = $.extend({
//            baseUrl: "/",
//            formSelector: "#leaveType-form",
//            formContainer: ".js-leaveType-form-container",
//            gridSelector: "#leaveType-grid",
//            gridContainer: ".js-leaveType-grid-container",
//            editSelector: ".js-leaveType-edit",
//            saveSelector: ".js-leaveType-save",
//            selectAllSelector: "#leaveType-check-all",
//            deleteSelector: ".js-leaveType-delete-confirm",
//            deleteModal: "#leaveType-delete-modal",
//            finalDeleteSelector: ".js-leaveType-delete",
//            clearSelector: ".js-leaveType-clear",
//            topSelector: ".js-go",
//            decimalSelector: ".js-leaveType-decimalplaces",
//            maxDecimalPlace: 5,
//            showNagativeFormat: false,
//            availabilitySelector: ".js-leaveTyp-check-duplicateName",
//            haseFile: false,
//            quickAddSelector: ".js-quick-add",
//            quickAddModal: "#quickAddModal",
//            lastCodeSelector: '#lastCode',
//            load: function () { }
//        }, options);

//        var gridUrl = settings.baseUrl + "/grid";
//        var saveUrl = settings.baseUrl + "/setup";
//        var deleteUrl = settings.baseUrl + "/Delete";
//        var selectedItems = [];

//        $(() => {



//            initialize();


//            $("body").on('click', '.js-leaveType-save', function ()
//            {


//                validation();
//                $('#leaveType-form').submit(); 

//            });


//            $("body").on('submit', '#leaveType-form', function (e)
//            {
//                e.preventDefault();
//                var form = $(this)[0];
//                var formData = new FormData(form);
//                var actionUrl = $(this).attr('action');
//                $.ajax({
//                    type: 'POST',
//                    url: actionUrl,
//                    data: formData,
//                    contentType: false,
//                    processData: false,
//                    success: function (result) {
                      
//                        if (result.isDuplicate)
//                        {
//                            toastr.error(result.message);
//                        }
                      
//                        else if (result.noSavePermission)
//                        {
//                            toastr.error(result.message);
//                        }
                      
//                        else if (result.noUpdatePermission)
//                        {
//                            toastr.error(result.message);
//                        }
                       
//                        else if (result.isSuccess)
//                        {
                           
//                            $('.js-leaveType-grid-container').html(result.html);
//                            initialize(); 
//                            toastr.success(result.message);
//                        } else
//                        {
                           
//                            $('#leaveType-form').html(result);
//                        }
//                    },

//                    error: function ()
//                    {
//                        toastr.error('An error occurred while saving customer information data.');
//                    }
//                });
//            });


//            $("body").on('click', '.js-leaveType-edit', function ()
//            {
//                var id = $(this).data('id');
//                $.get('/LeaveTypes/Index', { id: id }, function (result)
//                {
                  
//                    $('#leaveType-form').html($(result).find('#leaveType-form').html());
//                    WEFDatePicker();
//                    $('.js-leaveType-save').html('<i class="fas fa-edit"></i> Update');
//                    $('#leaveType-form').attr('action', '/LeaveTypes/Setup'); 
//                }).fail(function ()
//                {
                   
//                    toastr.error('Error fetching leave type details. Please try again.');

//                });
//            }); 

           

//            //delete 
//            $("body").on('click', '#leaveType-check-all', function ()
//            {
//                $('.checkBox').prop('checked', $(this).prop('checked'));
//            });

            
//            // Handle the 'Delete Multiple' button click (opens the modal)
//            $("body").on('click', '.js-leaveType-delete-confirm', function (e)
//            {
//                e.preventDefault();
//                var selectedIds = [];

//                $('.checkBox:checked').each(function ()
//                {
//                    selectedIds.push($(this).val());
//                });

//                if (selectedIds.length === 0) {
//                    toastr.error('Please select at least one leave type to delete.');
//                    return;
//                }
//                $('#leaveType-delete-modal .js-leaveType-delete').data('ids', selectedIds);
//                $('#leaveType-delete-modal').modal('show');
//            });

           
//            $("body").on('click', '.js-leaveType-delete', function (e)
//            {
//                e.preventDefault();

//                var selectedIds = $(this).data('ids'); 

//                $.ajax({
//                    type: 'POST',
//                    url: '/LeaveTypes/Delete',
//                    contentType: 'application/json',
//                    data: JSON.stringify(selectedIds),
//                    success: function (result)
//                    {
//                        if (result.isSuccess === false)
//                        {
//                            toastr.error(result.message);
//                        } else
//                        {
                           
//                            $('.js-leaveType-grid-container').html(result);
//                            GenerateNextLeaveTypeCode();
//                            dataTableLeaveType();
//                            toastr.success('Selected leave types successfully deleted.');
//                        }
                      
//                        $('#leaveType-delete-modal').modal('hide');
//                    },
//                    error: function ()
//                    {
//                        toastr.error('An error occurred while deleting the selected leave types.');
                    
//                        $('#leaveType-delete-modal').modal('hide');
//                    }
//                });
//            });

//            //
//            //


//            // Scroll to top
//            $("body").on("click", settings.topSelector, function (e)
//            {
//                e.preventDefault();
//                $("html, body").animate({ scrollTop: 0 }, 500);
//            });

//            // Handle decimal input
//            $("body").on("keyup", settings.decimalSelector, function () {
//                var self = $(this);
//                showDecimalPlaces(self.val(), self.parent().find(".input-group-text"));
//            });



//             // Check availability
//            $("body").on("keyup", ".js-leaveTyp-check-duplicateName", function ()
//            {
//                var self = $(this);
//                let code = $(".js-leaveType-code").val(); 
//                let name = self.val();

//                $.ajax({
//                    url: '/LeaveTypes/CheckAvailability',
//                    method: "POST",
//                    data: { code: code, name: name },
//                    success: function (response)
//                    {
//                        console.log(response);
//                        if (response.isSuccess)
//                        {
//                            toastr.error(response.message);
//                        }
//                    }
//                });
//            });
//            //


//        });
        
               
        

       

//        // Initialize the plugin
//        function initialize()
//        {
//            WEFDatePicker();
//            GenerateNextLeaveTypeCode();
//            dataTableLeaveType();
//            loadLeaveTypesList();
//            LeavetypeResetForm();
//            showDates();
//        }

       
//        function loadLeaveTypesList()
//        {
//            $.ajax({
//                type: 'GET',
//                url: '/LeaveTypes/GeLeaveTypesList',
//                success: function (data)
//                {
//                    $('.js-leaveType-grid-container').html(data);  
//                    dataTableLeaveType(); 
//                },
//                error: function ()
//                {
//                    toastr.error('An error occurred while loading the leave type list.');
//                }
//            });
//        }

       

//        //WEFDate Picker
//        function WEFDatePicker()
//        {

//            $("#WefDate").datepicker({
//                dateFormat: 'dd/mm/yy', 
//                changeMonth: true,
//                changeYear: true,
//                yearRange: "-10:+0",
//                beforeShow: function (input, inst)
//                {
//                    $(this).toggleClass("placeholder-shown", !this.value);
//                },
//                onClose: function (dateText, inst) {
//                    $(this).toggleClass("placeholder-shown", !this.value);
//                },
//                onSelect: function (dateText) {
                   
//                    var formattedDate = $.datepicker.formatDate('mm/dd/yy', $(this).datepicker('getDate'));
//                    $('#WefHidden').val(formattedDate);
//                    $("#WefDate").valid();
//                }
//            });

//            var initialDate = $("#WefDate").val();
//            if (initialDate)
//            {
//                var formattedInitialDate = $.datepicker.formatDate('mm/dd/yy', $("#WefDate").datepicker('getDate'));
//                $('#WefHidden').val(formattedInitialDate); 
//            }

//            $(".datepicker").trigger("input");

//        }
//        //end WEFDatePicker

//        // Generate next leave type code function
//        function GenerateNextLeaveTypeCode()
//        {
//            $("#LeaveTypeCode").val('');
//            $.ajax({
//                type: 'GET',
//                url: '/LeaveTypes/GenerateNextLeaveTypeCode',
//                success: function (result)
//                {
//                    $('#LeaveTypeCode').val(result);
//                },
//                error: function ()
//                {
//                    toastr.error('An error occurred while generating the next code.');
//                }
//            });
//        }

//        // Data table initialization function
//        function dataTableLeaveType()
//        {
//            $('#leaveType-grid').DataTable({
//                responsive: true,
//                pageLength: 10,
//                destroy: true,
//                lengthMenu: [10, 25, 50, 100],
//            });
//        }
//        //end DataTable method

//        function LeavetypeResetForm()
//        {
//            $('#leaveType-form')[0].reset(); 
//            $('#Id').val('');
//            $('#LeaveTypeCode').val('');
//            $('#Name').val('').trigger('focus');
//            $('#ShortName').val('');
//            $('#RulePolicy').val('');
//            $('#NoOfDay').val('');
//            $('#Ymwd').val('');
//            $('#WefDate').val('');
//            $('#For').val('');
//            $('.js-leaveType-save').html('<i class="fas fa-save"></i> Save');
//            $('#creationDateContainer').addClass('d-none');
//            $('#lastUpdateDateContainer').addClass('d-none');
//            $('.text-danger').text('');
//            $('#leaveType-form').attr('action', '/LeaveTypes/Setup');
//            GenerateNextLeaveTypeCode();
//        }
//        $(document).on('click', '.js-leaveType-clear', function ()
//        {
//            LeavetypeResetForm();
//        });


//        //  function to show the fields when data is loaded
//        function showDates(Ldate, ModifyDate)
//        {
//            if (Ldate)
//            {
//                $('#creationDateContainer').removeClass('d-none');
//                $('#Ldate').text(Ldate); 
//            }
//            if (ModifyDate)
//            {
//                $('#lastUpdateDateContainer').removeClass('d-none');
//                $('#ModifyDate').text(ModifyDate); 
//            }
//        }

//        function validation()
//        {
//            var name = $("input[name='Name']").val();
//            var wef = $("#WefDate").val();

//            if (!name)
//            {
//                toastr.info('Leave type name is required');
//                return false; 
//            }

//            if (!wef)
//            {
//                toastr.info('W.E.F is required');
//                return false; 
//            }
//        }

//    }
//}(jQuery));



 
       
//(function ($) {
//    $.leaveType = function (options) {
//        // Default options
//        var settings = $.extend({
//            baseUrl: "/",
//            formSelector: "#leaveType-form",
//            formContainer: ".js-leaveType-form-container",
//            gridSelector: "#leaveType-grid",
//            gridContainer: ".js-leaveType-grid-container",
//            editSelector: ".js-leaveType-edit",
//            saveSelector: ".js-leaveType-save",
//            selectAllSelector: "#leaveType-check-all",
//            deleteSelector: ".js-leaveType-delete-confirm",
//            deleteModal: "#leaveType-delete-modal",
//            finalDeleteSelector: ".js-leaveType-delete",
//           // clearSelector: ".js-leaveType-clear",
//            topSelector: ".js-go",
//            decimalSelector: ".js-leaveType-decimalplaces",
//            maxDecimalPlace: 5,
//            showNagativeFormat: false,
//            availabilitySelector: ".js-leaveTyp-check-availability",
//            haseFile: false,
//            quickAddSelector: ".js-quick-add",
//            quickAddModal: "#quickAddModal",
//            lastCodeSelector: '#lastCode',
//            load: function () { }
//        }, options);

//        var gridUrl = settings.baseUrl + "/grid";
//        var saveUrl = settings.baseUrl + "/setup";
//        var deleteUrl = settings.baseUrl + "/Delete";
//        var selectedItems = [];

//        $(() => {
           
//            initialize();

//            // Edit and clear button click events
//           // $("body").on("click", `${settings.editSelector},${settings.clearSelector}`, function (e)
//            $("body").on("click", `${settings.editSelector}`, function (e)
//            {
//                e.stopPropagation();
//                e.preventDefault();
//                e.stopImmediatePropagation();

//                let url = saveUrl + "/" + ($(this).data("id") ?? "");
//                loadForm(url);

//                $("html, body").animate({ scrollTop: 0 }, 500);
//            });

//            // Save button click event
//            $("body").on("click", settings.saveSelector, function ()
//            {

//                var name = $("input[name='Name']").val();
//                if (!name) {
//                    toastr.info('Leave type name is required');
//                    return false;
//                }

//                // Validate "Wef" field next
//                var wef = $("#WefDate").val();
//                if (!wef) {
//                    toastr.info('W.E.F is required');
//                    return false;
//                }


//                var $valid = $(settings.formSelector).valid();
//                if (!$valid)
//                {
//                    return false;
//                }

//                var data;
//                if (settings.haseFile)
//                    data = new FormData($(settings.formSelector)[0]);
//                else
//                    data = $(settings.formSelector).serialize();

//                var url = $(settings.formSelector).attr("action");

//                var options = {
//                    url: url,
//                    method: "POST",
//                    data: data,
//                    success: function (response) {
//                        if (response.isSuccess) {
//                            loadForm(saveUrl)
//                                .then((data) => {
//                                    $(settings.lastCodeSelector).val(response.lastCode);
//                                })
//                                .catch((error) => {
//                                    console.log(error);
//                                });
//                           // toastr.success( 'Success');
//                            toastr.success(response.success, 'Success');
//                        } else {
//                            toastr.error(response.message, 'Error');
//                            console.log(response);
//                        }
//                    }
//                };
//                if (settings.haseFile) {
//                    options.processData = false;
//                    options.contentType = false;
//                }
//                $.ajax(options);
//            });

//            // Handle 'Select All' checkbox
//            $("body").on("click", settings.selectAllSelector, function ()
//            {
//                $(".checkBox").prop('checked', $(this).prop('checked'));

//            });

//            // Delete button click event
//            $("body").on("click", settings.deleteSelector, function (e)
//            {
//                e.preventDefault();
//                $('input:checkbox.checkBox').each(function ()
//                {
//                    if ($(this).prop('checked')) {
//                        if (!selectedItems.includes($(this).val()))
//                        {
//                            selectedItems.push($(this).val());
//                        }
//                    }
//                });

//                if (selectedItems.length > 0)
//                {
//                    $(settings.deleteModal).modal("show");
//                } else
//                {
//                    toastr.info("Please select at least one item.", 'Warning');
//                }
//            });

//            // Show delete modal event
//            $("body").on('show.bs.modal', settings.deleteModal, function (event)
//            {
//                var source = $(event.relatedTarget);
//                var id = source.data("id");

//                // Extract value from data-* attributes
//                var title = source.data("title");
//                title = "Are you sure you want to delete these items?";
//                var modal = $(this);
//                $(modal).find('.title').html(title);

//                // Final delete action
//                $("body").on("click", settings.finalDeleteSelector, function (e)
//                {
//                    e.stopPropagation();
//                    e.preventDefault();
//                    e.stopImmediatePropagation();

//                    // Delete
//                    $.ajax({
//                        url: deleteUrl + "?ids=" + selectedItems.join(","),
//                        method: "POST",
//                        success: function (response) {
//                            console.log(response);
//                            $(modal).modal("hide");
//                            if (response.success) {
//                                toastr.success(response.message, 'Success');
//                                LeavetypeResetForm();
//                                selectedItems = [];
//                                loadForm(saveUrl);
//                            } else {
//                                toastr.error(response.message, 'Error');
//                                console.log(response);
//                            }
//                        }
//                    });
//                });
//            }).on('hide.bs.modal', function ()
//            {
//                $("body").off("click", settings.finalDeleteSelector);
//            });

//            // Scroll to top
//            $("body").on("click", settings.topSelector, function (e)
//            {
//                e.preventDefault();
//                $("html, body").animate({ scrollTop: 0 }, 500);
//            });

//            // Handle decimal input
//            $("body").on("keyup", settings.decimalSelector, function () {
//                var self = $(this);
//                showDecimalPlaces(self.val(), self.parent().find(".input-group-text"));
//            });

//            // Check availability
//            $("body").on("keyup", settings.availabilitySelector, function ()
//            {
//                var self = $(this);
//                let code = $(".js-leaveType-code").val();
//                let name = self.val();

//                $.ajax({
//                    url: settings.baseUrl + "/CheckAvailability",
//                    method: "POST",
//                    data: { code: code, name: name },
//                    success: function (response)
//                    {
//                        console.log(response);
//                        if (response.isSuccess)
//                        {
//                            toastr.error(response.message);
//                        }
//                    }
//                });
//            });
//        });
        
               
        

//        // Load form function
//        function loadForm(url) {
//            return new Promise((resolve, reject) => {
//                $.ajax({
//                    url: url,
//                    type: 'GET',
//                    success: function (data) {
//                        $(settings.formContainer).empty();
//                        $(settings.formContainer).html(data);
//                        $.validator.unobtrusive.parse($(settings.formSelector));
//                        initialize();
//                        resolve(data);
//                    },
//                    error: function (error) {
//                        reject(error);
//                    },
//                });
//            });
//        }

//        // Initialize the plugin
//        function initialize()
//        {
//            WEFDatePicker();
//           // GenerateNextLeaveTypeCode();
//            if ($('#LeaveTypeCode').val() === "")
//            {
//                GenerateNextLeaveTypeCode();
//            }
//            dataTableLeaveType();
//            loadLeaveTypesList();
//            // LeavetypeResetForm();
//            showDates();
//        }

       
//        function loadLeaveTypesList()
//        {
//            $.ajax({
//                type: 'GET',
//                url: '/LeaveTypes/GeLeaveTypesList',
//                success: function (data)
//                {
//                    $('.js-leaveType-grid-container').html(data);  
//                    dataTableLeaveType(); 
//                },
//                error: function ()
//                {
//                    toastr.error('An error occurred while loading the leave type list.');
//                }
//            });
//        }


//        //WEFDate Picker
//        function WEFDatePicker()
//        {

//            //

//            $("#WefDate").datepicker({
//                dateFormat: 'dd/mm/yy', 
//                changeMonth: true,
//                changeYear: true,
//                yearRange: "-10:+0",
//                beforeShow: function (input, inst) {
//                    $(this).toggleClass("placeholder-shown", !this.value);
//                },
//                onClose: function (dateText, inst) {
//                    $(this).toggleClass("placeholder-shown", !this.value);
//                },
//                onSelect: function (dateText) {
                   
//                    var formattedDate = $.datepicker.formatDate('mm/dd/yy', $(this).datepicker('getDate'));
//                    $('#WefHidden').val(formattedDate);
//                }
//            });

//            $(".datepicker").trigger("input");
//        }
//        //end WEFDatePicker

//        // Generate next leave type code function
//        function GenerateNextLeaveTypeCode()
//        {
//            $("#LeaveTypeCode").val('');
//            $.ajax({
//                type: 'GET',
//                url: '/LeaveTypes/GenerateNextLeaveTypeCode',
//                success: function (result) {
//                    $('#LeaveTypeCode').val(result);
//                },
//                error: function () {
//                    toastr.error('An error occurred while generating the next code.');
//                }
//            });
//        }

//        // Data table initialization function
//        function dataTableLeaveType()
//        {
//            $('#leaveType-grid').DataTable({
//                responsive: true,
//                pageLength: 10,
//                destroy: true,
//                lengthMenu: [10, 25, 50, 100],
//            });
//        }
//        //end DataTable method

//        function LeavetypeResetForm()
//        {
//            $('#leaveType-form')[0].reset();
          
//            $('#Id').val('');
//           // $('#LeaveTypeCode').val('');
//            $('#Name').val('').trigger('focus');
//            $('#ShortName').val('');
//            $('#RulePolicy').val('');
//            $('#NoOfDay').val('');
//            $('#Wef').val('');
//            $('#Ymwd').val('');
//            $('#Wef').val('');
//            $('#For').val('');
//            $('#creationDateContainer').addClass('d-none');
//            $('#lastUpdateDateContainer').addClass('d-none');
//            $('.text-danger').text('');
         

//        }
//        $(document).on('click', '.js-leaveType-clear', function ()
//        {
//            LeavetypeResetForm();
//        });


//        // Example function to show the fields when data is loaded
//        function showDates(Ldate, ModifyDate)
//        {
//            if (Ldate)
//            {
//                $('#creationDateContainer').removeClass('d-none');
//                $('#Ldate').text(Ldate); // Set the creation date
//            }
//            if (ModifyDate) {
//                $('#lastUpdateDateContainer').removeClass('d-none');
//                $('#ModifyDate').text(ModifyDate); // Set the modification date
//            }
//        }


//    }
//}(jQuery));



 
       