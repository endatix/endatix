﻿// <auto-generated />
using System;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Endatix.Core.Entities.Form", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<long?>("ActiveDefinitionId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("bit");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<long>("TenantId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ActiveDefinitionId")
                        .IsUnique()
                        .HasFilter("[ActiveDefinitionId] IS NOT NULL");

                    b.HasIndex("TenantId");

                    b.ToTable("Forms", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.FormDefinition", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<long>("FormId")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDraft")
                        .HasColumnType("bit");

                    b.Property<string>("JsonData")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("datetime2");

                    b.Property<long>("TenantId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("FormId");

                    b.HasIndex("TenantId");

                    b.ToTable("FormDefinitions", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.Submission", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int?>("CurrentPage")
                        .HasColumnType("int");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<long>("FormDefinitionId")
                        .HasColumnType("bigint");

                    b.Property<long>("FormId")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsComplete")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("JsonData")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Metadata")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("datetime2");

                    b.Property<long>("TenantId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("FormDefinitionId");

                    b.HasIndex("FormId");

                    b.HasIndex("TenantId");

                    b.ToTable("Submissions", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.Tenant", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("SlackSettingsJson")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Tenants", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.Form", b =>
                {
                    b.HasOne("Endatix.Core.Entities.FormDefinition", "ActiveDefinition")
                        .WithOne()
                        .HasForeignKey("Endatix.Core.Entities.Form", "ActiveDefinitionId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("Endatix.Core.Entities.Tenant", "Tenant")
                        .WithMany("Forms")
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("ActiveDefinition");

                    b.Navigation("Tenant");
                });

            modelBuilder.Entity("Endatix.Core.Entities.FormDefinition", b =>
                {
                    b.HasOne("Endatix.Core.Entities.Form", null)
                        .WithMany("FormDefinitions")
                        .HasForeignKey("FormId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("Endatix.Core.Entities.Tenant", "Tenant")
                        .WithMany("FormDefinitions")
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Tenant");
                });

            modelBuilder.Entity("Endatix.Core.Entities.Submission", b =>
                {
                    b.HasOne("Endatix.Core.Entities.FormDefinition", "FormDefinition")
                        .WithMany("Submissions")
                        .HasForeignKey("FormDefinitionId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("Endatix.Core.Entities.Tenant", "Tenant")
                        .WithMany("Submissions")
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.OwnsOne("Endatix.Core.Entities.SubmissionStatus", "Status", b1 =>
                        {
                            b1.Property<long>("SubmissionId")
                                .HasColumnType("bigint");

                            b1.Property<string>("Code")
                                .IsRequired()
                                .HasMaxLength(16)
                                .HasColumnType("nvarchar(16)")
                                .HasColumnName("Status");

                            b1.HasKey("SubmissionId");

                            b1.ToTable("Submissions");

                            b1.WithOwner()
                                .HasForeignKey("SubmissionId");
                        });

                    b.OwnsOne("Endatix.Core.Entities.Token", "Token", b1 =>
                        {
                            b1.Property<long>("SubmissionId")
                                .HasColumnType("bigint");

                            b1.Property<DateTime>("ExpiresAt")
                                .HasColumnType("datetime2");

                            b1.Property<string>("Value")
                                .IsRequired()
                                .HasMaxLength(64)
                                .HasColumnType("nvarchar(64)");

                            b1.HasKey("SubmissionId");

                            b1.HasIndex("Value");

                            b1.ToTable("Submissions");

                            b1.WithOwner()
                                .HasForeignKey("SubmissionId");
                        });

                    b.Navigation("FormDefinition");

                    b.Navigation("Status")
                        .IsRequired();

                    b.Navigation("Tenant");

                    b.Navigation("Token");
                });

            modelBuilder.Entity("Endatix.Core.Entities.Form", b =>
                {
                    b.Navigation("FormDefinitions");
                });

            modelBuilder.Entity("Endatix.Core.Entities.FormDefinition", b =>
                {
                    b.Navigation("Submissions");
                });

            modelBuilder.Entity("Endatix.Core.Entities.Tenant", b =>
                {
                    b.Navigation("FormDefinitions");

                    b.Navigation("Forms");

                    b.Navigation("Submissions");
                });
#pragma warning restore 612, 618
        }
    }
}
