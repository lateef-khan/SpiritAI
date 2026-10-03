namespace SpiritAI.Twenty;

/// <summary>Twenty would not remove the member: it is the workspace's last admin.</summary>
public sealed class CrmRefusedException()
    : Exception("CRM will not remove its last admin. Make another CRM admin first.");
