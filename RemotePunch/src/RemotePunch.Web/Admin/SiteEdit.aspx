<%@ Page Title="Site" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="SiteEdit.aspx.cs" Inherits="RemotePunch.Web.Admin.SiteEditPage" %>

<asp:Content ContentPlaceHolderID="head" runat="server">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css"
          crossorigin="anonymous" referrerpolicy="no-referrer" />
</asp:Content>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1><asp:Literal ID="litTitle" runat="server" /></h1>

    <asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
        <div class="alert alert-bad"><asp:Literal ID="litMessage" runat="server" /></div>
    </asp:PlaceHolder>

    <div class="grid grid-2">
        <div class="card">
            <div class="field">
                <label for="<%= txtName.ClientID %>">Site name</label>
                <asp:TextBox ID="txtName" runat="server" MaxLength="120" />
            </div>
            <div class="field">
                <label for="<%= txtAddress.ClientID %>">Address</label>
                <asp:TextBox ID="txtAddress" runat="server" TextMode="MultiLine" MaxLength="400" />
            </div>
            <div class="grid grid-2">
                <div class="field">
                    <label for="<%= txtLatitude.ClientID %>">Latitude</label>
                    <asp:TextBox ID="txtLatitude" runat="server" MaxLength="20" />
                </div>
                <div class="field">
                    <label for="<%= txtLongitude.ClientID %>">Longitude</label>
                    <asp:TextBox ID="txtLongitude" runat="server" MaxLength="20" />
                </div>
            </div>
            <div class="field">
                <label for="<%= txtRadius.ClientID %>">Radius in metres (25 - 100000)</label>
                <asp:TextBox ID="txtRadius" runat="server" TextMode="Number" MaxLength="6" Text="150" />
                <div class="hint">
                    150 m suits a single building. Go wider for a campus, but every
                    extra metre is somewhere a punch will be accepted from.
                </div>
            </div>
            <div class="checkline">
                <asp:CheckBox ID="chkActive" runat="server" Checked="true" />
                <label for="<%= chkActive.ClientID %>" style="margin:0">Site is active</label>
            </div>

            <div class="actions">
                <asp:Button ID="btnSave" runat="server" Text="Save site" CssClass="btn btn-primary" OnClick="btnSave_Click" />
                <a class="btn btn-ghost" href="<%= ResolveUrl("~/Admin/Sites.aspx") %>">Back to list</a>
            </div>
        </div>

        <div class="card">
            <h2>Pick the centre</h2>
            <p class="small muted">Click the map to move the centre, or use your own position.</p>
            <div id="map"></div>
            <div class="actions" style="margin-top:12px">
                <button type="button" id="btnUseMyLocation" class="btn btn-ghost btn-sm">Use my current location</button>
            </div>
        </div>
    </div>

    <script src="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js"
            crossorigin="anonymous" referrerpolicy="no-referrer"></script>
    <script>
        window.RP_SITE = {
            latId: '<%= txtLatitude.ClientID %>',
            lngId: '<%= txtLongitude.ClientID %>',
            radiusId: '<%= txtRadius.ClientID %>'
        };
    </script>
    <script src="<%= ResolveUrl("~/Scripts/site-picker.js") %>"></script>
</asp:Content>
