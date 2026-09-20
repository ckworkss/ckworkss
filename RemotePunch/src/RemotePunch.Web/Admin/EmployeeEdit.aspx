<%@ Page Title="Employee" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="EmployeeEdit.aspx.cs" Inherits="RemotePunch.Web.Admin.EmployeeEditPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1><asp:Literal ID="litTitle" runat="server" /></h1>

    <asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
        <div class="alert <%= MessageClass %>"><asp:Literal ID="litMessage" runat="server" /></div>
    </asp:PlaceHolder>

    <div class="grid grid-2">
        <div class="card">
            <h2>Details</h2>
            <div class="field">
                <label for="<%= txtCode.ClientID %>">Employee code</label>
                <asp:TextBox ID="txtCode" runat="server" MaxLength="40" />
            </div>
            <div class="field">
                <label for="<%= txtName.ClientID %>">Full name</label>
                <asp:TextBox ID="txtName" runat="server" MaxLength="160" />
            </div>
            <div class="field">
                <label for="<%= txtEmail.ClientID %>">Email</label>
                <asp:TextBox ID="txtEmail" runat="server" TextMode="Email" MaxLength="200" />
            </div>
            <div class="field">
                <label for="<%= txtPhone.ClientID %>">Phone</label>
                <asp:TextBox ID="txtPhone" runat="server" MaxLength="40" />
            </div>
            <div class="field">
                <label for="<%= ddlRole.ClientID %>">Role</label>
                <asp:DropDownList ID="ddlRole" runat="server">
                    <asp:ListItem Text="Employee" Value="Employee" />
                    <asp:ListItem Text="Manager (sees reports)" Value="Manager" />
                    <asp:ListItem Text="Admin (full access)" Value="Admin" />
                </asp:DropDownList>
            </div>
        </div>

        <div class="card">
            <h2>Attendance policy</h2>
            <div class="grid grid-2">
                <div class="field">
                    <label for="<%= txtShiftStart.ClientID %>">Shift start</label>
                    <asp:TextBox ID="txtShiftStart" runat="server" TextMode="Time" />
                </div>
                <div class="field">
                    <label for="<%= txtShiftEnd.ClientID %>">Shift end</label>
                    <asp:TextBox ID="txtShiftEnd" runat="server" TextMode="Time" />
                </div>
            </div>

            <div class="checkline">
                <asp:CheckBox ID="chkActive" runat="server" Checked="true" />
                <label for="<%= chkActive.ClientID %>" style="margin:0">Account is active</label>
            </div>
            <div class="checkline">
                <asp:CheckBox ID="chkRemote" runat="server" />
                <label for="<%= chkRemote.ClientID %>" style="margin:0">
                    Allow punching away from an assigned site (goes to the review queue)
                </label>
            </div>
            <div class="checkline">
                <asp:CheckBox ID="chkSelfie" runat="server" />
                <label for="<%= chkSelfie.ClientID %>" style="margin:0">Require a photo with every punch</label>
            </div>

            <h3 style="margin-top:18px">Sites this employee may punch from</h3>
            <p class="small muted">Tick none to allow every active site.</p>
            <asp:CheckBoxList ID="chkSites" runat="server" CssClass="checkline" RepeatLayout="Flow" />
        </div>
    </div>

    <div class="card">
        <h2>Password</h2>
        <div class="filters">
            <div class="field">
                <label for="<%= txtPassword.ClientID %>"><asp:Literal ID="litPasswordLabel" runat="server" /></label>
                <asp:TextBox ID="txtPassword" runat="server" TextMode="Password" autocomplete="new-password" />
                <div class="hint">At least 10 characters. The employee is asked to change it at first sign-in.</div>
            </div>
            <div class="actions">
                <asp:Button ID="btnUnlock" runat="server" Text="Clear lockout" CssClass="btn btn-ghost"
                            OnClick="btnUnlock_Click" />
            </div>
        </div>
    </div>

    <div class="card">
        <div class="actions">
            <asp:Button ID="btnSave" runat="server" Text="Save" CssClass="btn btn-primary" OnClick="btnSave_Click" />
            <a class="btn btn-ghost" href="<%= ResolveUrl("~/Admin/Employees.aspx") %>">Back to list</a>
        </div>
    </div>
</asp:Content>
