using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MelancholyNotes
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<Note> allNotes;
        private Note currentNote;
        private string dbPath;
        private bool isUpdating = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadNotes();
        }

        // Методы для управления окном
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Позволяет перетаскивать окно за любое место
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void InitializeDatabase()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MelancholyNotes"
            );
            Directory.CreateDirectory(appData);
            dbPath = Path.Combine(appData, "notes.db");

            using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                conn.Open();
                string createTable = @"
                    CREATE TABLE IF NOT EXISTS Notes (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Content TEXT,
                        CreatedDate TEXT NOT NULL,
                        ModifiedDate TEXT NOT NULL
                    )";
                using (var cmd = new SQLiteCommand(createTable, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void LoadNotes()
        {
            allNotes = new ObservableCollection<Note>();

            using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                conn.Open();
                string query = "SELECT * FROM Notes ORDER BY ModifiedDate DESC";
                using (var cmd = new SQLiteCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        allNotes.Add(new Note
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Title = reader["Title"].ToString(),
                            Content = reader["Content"].ToString(),
                            CreatedDate = DateTime.Parse(reader["CreatedDate"].ToString()),
                            ModifiedDate = DateTime.Parse(reader["ModifiedDate"].ToString())
                        });
                    }
                }
            }

            NotesListBox.ItemsSource = allNotes;
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            currentNote = new Note
            {
                Title = "Новая заметка",
                Content = "",
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            };

            using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                conn.Open();
                string insert = @"INSERT INTO Notes (Title, Content, CreatedDate, ModifiedDate) 
                                VALUES (@title, @content, @created, @modified)";
                using (var cmd = new SQLiteCommand(insert, conn))
                {
                    cmd.Parameters.AddWithValue("@title", currentNote.Title);
                    cmd.Parameters.AddWithValue("@content", currentNote.Content);
                    cmd.Parameters.AddWithValue("@created", currentNote.CreatedDate.ToString("o"));
                    cmd.Parameters.AddWithValue("@modified", currentNote.ModifiedDate.ToString("o"));
                    cmd.ExecuteNonQuery();
                    currentNote.Id = (int)conn.LastInsertRowId;
                }
            }

            allNotes.Insert(0, currentNote);
            NotesListBox.SelectedItem = currentNote;
            UpdateEditor();
            TitleBox.Focus();
            TitleBox.SelectAll();
        }

        private void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (currentNote == null) return;

            currentNote.Title = string.IsNullOrWhiteSpace(TitleBox.Text)
                ? "Без названия"
                : TitleBox.Text;
            currentNote.Content = ContentBox.Text;
            currentNote.ModifiedDate = DateTime.Now;

            using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                conn.Open();
                string update = @"UPDATE Notes 
                                SET Title = @title, Content = @content, ModifiedDate = @modified 
                                WHERE Id = @id";
                using (var cmd = new SQLiteCommand(update, conn))
                {
                    cmd.Parameters.AddWithValue("@title", currentNote.Title);
                    cmd.Parameters.AddWithValue("@content", currentNote.Content);
                    cmd.Parameters.AddWithValue("@modified", currentNote.ModifiedDate.ToString("o"));
                    cmd.Parameters.AddWithValue("@id", currentNote.Id);
                    cmd.ExecuteNonQuery();
                }
            }

            // Перемещаем заметку наверх списка
            allNotes.Remove(currentNote);
            allNotes.Insert(0, currentNote);
            NotesListBox.SelectedItem = currentNote;

            MessageBox.Show("✅ Заметка сохранена!", "Melancholy Notes",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (currentNote == null) return;

            var result = MessageBox.Show(
                "Вы уверены, что хотите удалить эту заметку?",
                "Удаление заметки",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();
                    string delete = "DELETE FROM Notes WHERE Id = @id";
                    using (var cmd = new SQLiteCommand(delete, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", currentNote.Id);
                        cmd.ExecuteNonQuery();
                    }
                }

                allNotes.Remove(currentNote);
                currentNote = null;
                ClearEditor();
            }
        }

        private void NotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotesListBox.SelectedItem is Note selected)
            {
                currentNote = selected;
                UpdateEditor();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                NotesListBox.ItemsSource = allNotes;
            }
            else
            {
                var filtered = allNotes.Where(n =>
                    n.Title.ToLower().Contains(searchText) ||
                    n.Content.ToLower().Contains(searchText)
                ).ToList();
                NotesListBox.ItemsSource = filtered;
            }
        }

        private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (currentNote != null && !isUpdating)
            {
                currentNote.Title = TitleBox.Text;
            }
        }

        private void ContentBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (currentNote != null && !isUpdating)
            {
                currentNote.Content = ContentBox.Text;
            }
        }

        private void UpdateEditor()
        {
            if (currentNote != null)
            {
                isUpdating = true;
                TitleBox.Text = currentNote.Title;
                ContentBox.Text = currentNote.Content;
                isUpdating = false;
            }
        }

        private void ClearEditor()
        {
            isUpdating = true;
            TitleBox.Clear();
            ContentBox.Clear();
            isUpdating = false;
        }
    }

    public class Note
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        public string Preview
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content))
                    return "Пустая заметка";
                return Content.Length > 60
                    ? Content.Substring(0, 60) + "..."
                    : Content;
            }
        }
    }
}