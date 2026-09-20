<%@ Page Title="Review queue" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Review.aspx.cs" Inherits="RemotePunch.Web.Admin.ReviewPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>Review queue</h1>
    <p class="muted">
        Flagged punches are already counted in attendance. Approving one clears the
        flag; declining leaves it on the record with your note.
    </p>

    <asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
        <div class="alert alert-ok"><asp:Literal ID="litMessage" runat="server" /></div>
    </asp:PlaceHolder>

    <asp:Repeater ID="rptQueue" runat="server" OnItemCommand="rptQueue_ItemCommand">
        <ItemTemplate>
            <div class="card">
                <div class="grid grid-2">
                    <div>
                        <h2>
                            <%# PageHelpers.TextOrDash(Eval("EmployeeName")) %>
                            <span class="badge badge-warn"><%# Eval("PunchType") %></span>
                        </h2>
                        <div class="readout-row"><span class="k">When</span><span class="v"><%# Eval("PunchTimeLocal", "{0:dd MMM yyyy, HH:mm:ss}") %></span></div>
                        <div class="readout-row"><span class="k">Nearest site</span><span class="v"><%# PageHelpers.TextOrDash(Eval("SiteName")) %></span></div>
                        <div class="readout-row"><span class="k">Distance</span><span class="v"><%# PageHelpers.DistanceText(Eval("DistanceMeters")) %></span></div>
                        <div class="readout-row"><span class="k">Accuracy</span><span class="v"><%# PageHelpers.AccuracyText(Eval("AccuracyMeters")) %></span></div>
                        <div class="readout-row"><span class="k">Risk score</span><span class="v"><%# Eval("RiskScore") %></span></div>
                        <div class="readout-row"><span class="k">Where</span><span class="v"><%# PageHelpers.MapLink(Eval("Latitude"), Eval("Longitude"), Eval("ResolvedAddress")) %></span></div>
                        <div class="readout-row"><span class="k">Device</span><span class="v mono small"><%# ShortFingerprint(Eval("DeviceFingerprint")) %></span></div>
                        <div class="readout-row"><span class="k">IP address</span><span class="v mono small"><%# PageHelpers.TextOrDash(Eval("IpAddress")) %></span></div>

                        <div class="alert alert-warn small" style="margin-top:12px">
                            <%# FlagList(Container.DataItem) %>
                        </div>

                        <asp:PlaceHolder ID="phNote" runat="server" Visible='<%# !string.IsNullOrEmpty(Convert.ToString(Eval("Note"))) %>'>
                            <p class="small"><strong>Employee note:</strong> <%# PageHelpers.Encode(Convert.ToString(Eval("Note"))) %></p>
                        </asp:PlaceHolder>
                    </div>

                    <div>
                        <asp:PlaceHolder ID="phSelfie" runat="server" Visible='<%# Convert.ToBoolean(Eval("HasSelfie")) %>'>
                            <img src='<%# ResolveUrl("~/Api/Selfie.ashx?punchId=") + Eval("PunchId") %>'
                                 alt="Punch photo" style="width:100%;border-radius:12px;max-height:260px;object-fit:cover" />
                        </asp:PlaceHolder>

                        <div class="field" style="margin-top:12px">
                            <label>Review note</label>
                            <asp:TextBox ID="txtNote" runat="server" TextMode="MultiLine" MaxLength="400" />
                        </div>
                        <div class="actions">
                            <asp:LinkButton ID="btnApprove" runat="server" CssClass="btn btn-primary btn-sm"
                                CommandName="approve" CommandArgument='<%# Eval("PunchId") %>' Text="Approve" />
                            <asp:LinkButton ID="btnDecline" runat="server" CssClass="btn btn-danger btn-sm"
                                CommandName="decline" CommandArgument='<%# Eval("PunchId") %>' Text="Decline" />
                        </div>
                    </div>
                </div>
            </div>
        </ItemTemplate>
    </asp:Repeater>

    <asp:PlaceHolder ID="phEmpty" runat="server" Visible="false">
        <div class="card"><div class="empty">Nothing is waiting for review.</div></div>
    </asp:PlaceHolder>
</asp:Content>
