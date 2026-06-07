using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Minark.Server.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "emoji",
                table: "message_reactions",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_bin",
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_unread_messages_recipient_id_from_username",
                table: "unread_messages",
                columns: new[] { "recipient_id", "from_username" });

            migrationBuilder.CreateIndex(
                name: "IX_news_reactions_news_id",
                table: "news_reactions",
                column: "news_id");

            migrationBuilder.CreateIndex(
                name: "IX_news_author",
                table: "news",
                column: "author");

            migrationBuilder.CreateIndex(
                name: "IX_news_published_at",
                table: "news",
                column: "published_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_message_reactions_message_id",
                table: "message_reactions",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_friendships_status",
                table: "friendships",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_is_deleted",
                table: "chat_messages",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_sender_id_receiver_id_sent_at",
                table: "chat_messages",
                columns: new[] { "sender_id", "receiver_id", "sent_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_sent_at",
                table: "chat_messages",
                column: "sent_at",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_unread_messages_recipient_id_from_username",
                table: "unread_messages");

            migrationBuilder.DropIndex(
                name: "IX_news_reactions_news_id",
                table: "news_reactions");

            migrationBuilder.DropIndex(
                name: "IX_news_author",
                table: "news");

            migrationBuilder.DropIndex(
                name: "IX_news_published_at",
                table: "news");

            migrationBuilder.DropIndex(
                name: "IX_message_reactions_message_id",
                table: "message_reactions");

            migrationBuilder.DropIndex(
                name: "IX_friendships_status",
                table: "friendships");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_is_deleted",
                table: "chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_sender_id_receiver_id_sent_at",
                table: "chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_sent_at",
                table: "chat_messages");

            migrationBuilder.AlterColumn<string>(
                name: "emoji",
                table: "message_reactions",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_bin")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
