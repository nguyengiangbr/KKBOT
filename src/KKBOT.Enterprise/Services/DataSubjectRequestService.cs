// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Enterprise.Abstractions;
using KKBOT.Enterprise.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Enterprise.Services;

public sealed class DataSubjectRequestService : IDataSubjectRequestService
{
    private readonly EnterpriseDbContext _dbContext;
    private readonly ILogger<DataSubjectRequestService> _logger;

    public DataSubjectRequestService(EnterpriseDbContext dbContext, ILogger<DataSubjectRequestService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DataSubjectRequest> CreateRequestAsync(CreateDataSubjectRequestRequest request, CancellationToken ct = default)
    {
        var requestId = $"DSR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        
        var dsr = new DataSubjectRequest
        {
            RequestId = requestId,
            Type = request.Type,
            DataSubjectId = request.DataSubjectId,
            DataSubjectEmail = request.DataSubjectEmail,
            DataSubjectName = request.DataSubjectName,
            LegalBasis = request.LegalBasis,
            Description = request.Description,
            DataCategories = request.DataCategories,
            ProcessingPurposes = request.ProcessingPurposes,
            DataControllers = request.DataControllers,
            DataProcessors = request.DataProcessors,
            ThirdCountries = request.ThirdCountries,
            Safeguards = request.Safeguards,
            RequiresIdentityVerification = request.RequiresIdentityVerification,
            DueDate = DateTime.UtcNow.AddDays(30), // GDPR requires response within 30 days
            Status = DataSubjectRequestStatus.Submitted
        };

        _dbContext.DataSubjectRequests.Add(dsr);
        await _dbContext.SaveChangesAsync(ct);

        // Log audit event
        // await _auditService.LogAsync(new AuditLogEntry { ... });

        return dsr;
    }

    public async Task<DataSubjectRequest?> GetRequestAsync(Guid id, string userId, CancellationToken ct = default)
    {
        return await _dbContext.DataSubjectRequests
            .Include(r => r.Attachments)
            .Include(r => r.Notes)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<DataSubjectRequest>> GetRequestsAsync(string userId, DataSubjectRequestFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.DataSubjectRequests
            .Include(r => r.Attachments)
            .AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(r => r.Type == filter.Type.Value);

        if (filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);

        if (!string.IsNullOrWhiteSpace(filter.DataSubjectId))
            query = query.Where(r => r.DataSubjectId == filter.DataSubjectId);

        if (!string.IsNullOrWhiteSpace(filter.DataSubjectEmail))
            query = query.Where(r => r.DataSubjectEmail.Contains(filter.DataSubjectEmail));

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.SubmittedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(r => r.SubmittedAt <= filter.ToDate.Value);

        if (filter.IsVerified.HasValue)
            query = query.Where(r => r.IsVerified == filter.IsVerified.Value);

        query = query.OrderByDescending(r => r.SubmittedAt);

        return await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<DataSubjectRequest> UpdateRequestAsync(Guid id, UpdateDataSubjectRequestRequest request, string userId, CancellationToken ct = default)
    {
        var dsr = await _dbContext.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (dsr == null)
            throw new InvalidOperationException("Request not found");

        if (request.Status.HasValue) dsr.Status = request.Status.Value;
        if (request.LegalBasis != null) dsr.LegalBasis = request.LegalBasis;
        if (request.Description != null) dsr.Description = request.Description;
        if (request.DataCategories != null) dsr.DataCategories = request.DataCategories;
        if (request.ProcessingPurposes != null) dsr.ProcessingPurposes = request.ProcessingPurposes;
        if (request.DataControllers != null) dsr.DataControllers = request.DataControllers;
        if (request.DataProcessors != null) dsr.DataProcessors = request.DataProcessors;
        if (request.ThirdCountries != null) dsr.ThirdCountries = request.ThirdCountries;
        if (request.Safeguards != null) dsr.Safeguards = request.Safeguards;
        if (request.AssignedTo != null) dsr.AssignedTo = request.AssignedTo;
        if (request.AssignedToEmail != null) dsr.AssignedToEmail = request.AssignedToEmail;
        if (request.LegalHoldTag != null) dsr.LegalHoldTag = request.LegalHoldTag;
        if (request.EscalationContact != null) dsr.EscalationContact = request.EscalationContact;

        await _dbContext.SaveChangesAsync(ct);
        return dsr;
    }

    public async Task<bool> DeleteRequestAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var dsr = await _dbContext.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (dsr == null) return false;

        _dbContext.DataSubjectRequests.Remove(dsr);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DataSubjectRequest> ProcessRequestAsync(Guid id, ProcessDataSubjectRequestRequest request, string userId, CancellationToken ct = default)
    {
        var dsr = await _dbContext.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (dsr == null)
            throw new InvalidOperationException("Request not found");

        dsr.Status = request.Status;
        if (request.RejectionReason != null) dsr.RejectionReason = request.RejectionReason;
        if (request.RejectionDetails != null) dsr.RejectionDetails = request.RejectionDetails;
        if (request.CompletionDetails != null) dsr.CompletionDetails = request.CompletionDetails;
        if (request.OutputPath != null) dsr.OutputPath = request.OutputPath;

        if (request.Status == DataSubjectRequestStatus.Completed)
        {
            dsr.CompletedAt = DateTime.UtcNow;
        }
        else if (request.Status == DataSubjectRequestStatus.Rejected)
        {
            dsr.RejectedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        return dsr;
    }

    public async Task<DataSubjectRequest> VerifyIdentityAsync(Guid id, VerifyIdentityRequest request, CancellationToken ct = default)
    {
        var dsr = await _dbContext.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (dsr == null)
            throw new InvalidOperationException("Request not found");

        dsr.IsVerified = true;
        dsr.VerifiedAt = DateTime.UtcNow;
        dsr.VerifiedBy = request.VerifiedBy;
        dsr.IdentityVerificationMethod = request.Method;

        await _dbContext.SaveChangesAsync(ct);
        return dsr;
    }

    public async Task<DataSubjectRequest> AddAttachmentAsync(Guid requestId, DataSubjectRequestAttachment attachment, CancellationToken ct = default)
    {
        var dsr = await _dbContext.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (dsr == null)
            throw new InvalidOperationException("Request not found");

        attachment.Id = Guid.NewGuid();
        dsr.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync(ct);
        return dsr;
    }

    public async Task<bool> RemoveAttachmentAsync(Guid requestId, Guid attachmentId, string userId, CancellationToken ct = default)
    {
        var dsr = await _dbContext.DataSubjectRequests
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (dsr == null) return false;

        var attachment = dsr.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment == null) return false;

        dsr.Attachments.Remove(attachment);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DataSubjectRequest> AddNoteAsync(Guid requestId, DataSubjectRequestNote note, string userId, CancellationToken ct = default)
    {
        var dsr = await _dbContext.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (dsr == null)
            throw new InvalidOperationException("Request not found");

        note.Id = Guid.NewGuid();
        note.AuthorId = userId;
        note.AuthorName = "User"; // Would get from user service
        note.CreatedAt = DateTime.UtcNow;

        dsr.Notes.Add(note);
        await _dbContext.SaveChangesAsync(ct);
        return dsr;
    }
}