﻿// <auto-generated />
using System;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Endatix.Persistence.PostgreSQL.Migrations.AppEntities
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250312210634_AddFormTemplates")]
    partial class AddFormTemplates
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Endatix.Core.Entities.Form", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<long?>("ActiveDefinitionId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<long>("TenantId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ActiveDefinitionId")
                        .IsUnique();

                    b.HasIndex("TenantId");

                    b.ToTable("Forms", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.FormDefinition", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("FormId")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsDraft")
                        .HasColumnType("boolean");

                    b.Property<string>("JsonData")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("TenantId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("FormId");

                    b.HasIndex("TenantId");

                    b.ToTable("FormDefinitions", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.FormTemplate", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("boolean");

                    b.Property<string>("JsonData")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<long>("TenantId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("TenantId");

                    b.ToTable("FormTemplates", (string)null);
                });

            modelBuilder.Entity("Endatix.Core.Entities.Submission", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("CurrentPage")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("FormDefinitionId")
                        .HasColumnType("bigint");

                    b.Property<long>("FormId")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsComplete")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean");

                    b.Property<string>("JsonData")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Metadata")
                        .HasColumnType("text");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("timestamp with time zone");

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
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("SlackSettingsJson")
                        .HasColumnType("text");

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

            modelBuilder.Entity("Endatix.Core.Entities.FormTemplate", b =>
                {
                    b.HasOne("Endatix.Core.Entities.Tenant", "Tenant")
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Cascade)
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
                                .HasColumnType("character varying(16)")
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
                                .HasColumnType("timestamp with time zone");

                            b1.Property<string>("Value")
                                .IsRequired()
                                .HasMaxLength(64)
                                .HasColumnType("character varying(64)");

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
