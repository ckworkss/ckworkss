<%@ Page Title="Employees" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Employees.aspx.cs" Inherits="RemotePunch.Web.Admin.EmployeesPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>Employees</h1>

    <div class="card">
        <div class="actions">
            <a class="btn btn-primary" href="<%= ResolveUrl("~/Admin/EmployeeEdit.aspx") %>">Add employee</a>
            <asp:CheckBox ID="chkIncludeInactive" runat="server" AutoPostBack="true"
                          Text=" Show inactive accounts" OnCheckedChanged="chkIncludeInactive_CheckedChanged" />
        </div>
    </div>

    <div class="card">
        <div class="table-wrap">
            <asp:Repeater ID="rptEmployees" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr>
                                <th>Code</th><th>Name</th><th>Email</th><th>Role</th><th>Shift</th>
                                <th>Remote</th><th>Photo</th><th>Status</th><th>Last sign-in</th><th></th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td class="mono"><%# PageHelpers.TextOrDash(Eval("EmployeeCode")) %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("FullName")) %></td>
                        <td class="small"><%# PageHelpers.TextOrDash(Eval("Email")) %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("Role")) %></td>
                        <td class="small"><%# ShiftText(Container.DataItem) %></td>
                        <td><%# YesNo(Eval("AllowRemotePunch")) %></td>
                        <td><%# YesNo(Eval("RequireSelfie")) %></td>
                        <td><%# StatusBadge(Container.DataItem) %></td>
                        <td class="small"><%# LastLogin(Eval("LastLoginUtc")) %></td>
                        <td><a class="btn btn-ghost btn-sm"
                               href='<%# ResolveUrl("~/Admin/EmployeeEdit.aspx?id=") + Eval("EmployeeId") %>'>Edit</a></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
        </div>
    </div>
</asp:Content>
