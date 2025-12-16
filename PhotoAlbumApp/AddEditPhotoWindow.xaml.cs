using Microsoft.Win32;
using PhotoAlbumApp.Models;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoAlbumApp
{
    public partial class AddEditPhotoWindow : Window
    {
        private PhotoAlbumDBEntities dbContext;
        private string sourceFilePath; //путь к файлу изображения

        //конструктор окна добавления/редактирования фото
        public AddEditPhotoWindow()
        {
            InitializeComponent();
            dbContext = new PhotoAlbumDBEntities(); //инициализация контекста БД

            // ЗАГРУЖАЕМ КАТЕГОРИИ ПРИ ИНИЦИАЛИЗАЦИИ ОКНА
            LoadCategories();
        }

        // МЕТОД ДЛЯ ЗАГРУЗКИ КАТЕГОРИЙ
        private void LoadCategories()
        {
            try
            {
                // Получаем все категории из базы данных
                var categories = dbContext.Categories.ToList();

                // Добавляем элемент "Без категории" или "Выберите категорию" в начало
                var categoriesList = categories.ToList();

                // Устанавливаем источник данных для ComboBox
                CategoryComboBox.ItemsSource = categoriesList;

                // Устанавливаем отображаемое поле (если не настроено в XAML)
                if (CategoryComboBox.DisplayMemberPath == null)
                {
                    CategoryComboBox.DisplayMemberPath = "Name";
                }

                // Если есть категории, выбираем первую по умолчанию
                if (CategoryComboBox.Items.Count > 0)
                {
                    CategoryComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки категорий: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //кнопка обзор
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Все файлы (*.*)|*.*";
            openFileDialog.Title = "Выберите фотографию";

            if (openFileDialog.ShowDialog() == true) //если файл выбран
            {
                sourceFilePath = openFileDialog.FileName; //сохраняем путь
                FilePathTextBox.Text = sourceFilePath; //показываем путь в TextBox

                //пытаемся загрузить изображение
                try
                {
                    PreviewImage.Source = new BitmapImage(new Uri(sourceFilePath));
                }
                catch
                {
                    PreviewImage.Source = null; //если не удалось - очищаем
                }
            }
        }

        //кнопка сохранить
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //проверяем пустая строка или только пробелы
                if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
                {
                    MessageBox.Show("Введите название фотографии",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TitleTextBox.Focus(); //устанавливаем курсор в поле названия
                    return;
                }

                if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                {
                    MessageBox.Show("Выберите файл фотографии",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                //СОЗДАНИЕ ОБЪЕКТА ФОТОГРАФИИ
                var photo = new Photos();
                photo.Title = TitleTextBox.Text.Trim(); //название
                photo.Description = DescriptionTextBox.Text.Trim(); //описание
                photo.CreatedDate = DateTime.Now; //текущая дата

                //копирование файла в папку Images приложения
                string imagesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");

                // Убедимся, что папка Images существует
                if (!Directory.Exists(imagesFolder))
                {
                    Directory.CreateDirectory(imagesFolder);
                }

                string fileName = Path.GetFileName(sourceFilePath); //имя файла
                string destinationPath = Path.Combine(imagesFolder, fileName); //полный путь

                //если файл уже существует - генерируем уникальное имя 
                if (File.Exists(destinationPath))
                {
                    string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    fileName = $"{nameWithoutExtension}_{timestamp}{extension}";
                    destinationPath = Path.Combine(imagesFolder, fileName);
                }

                File.Copy(sourceFilePath, destinationPath); //копируем файл в папку
                photo.FilePath = destinationPath; //сохраняем новый путь

                //получаем выбранную категорию из ComboBox
                var selectedCategory = CategoryComboBox.SelectedItem as Categories;
                if (selectedCategory != null)
                {
                    photo.CategoryId = selectedCategory.CategoryId; //устанавливаем ID категории
                }
                else
                {
                    photo.CategoryId = 1;
                }

                //ДОБАВЛЕНИЕ ТЕГОВ
                if (!string.IsNullOrWhiteSpace(TagsTextBox.Text))
                {
                    //разбиваем строку тегов по запятым, очищаем от пробелов
                    string[] tagNames = TagsTextBox.Text.Split(',')
                        .Select(t => t.Trim()) //убираем пробелы 
                        .Where(t => !string.IsNullOrEmpty(t)) //удаляем пустые теги
                        .ToArray();

                    foreach (string tagName in tagNames)
                    {
                        //ищем существующий тег в бд
                        var tag = dbContext.Tags.FirstOrDefault(t => t.Name == tagName);
                        if (tag == null) //если не найден - создаем новый
                        {
                            tag = new Tags { Name = tagName };
                            dbContext.Tags.Add(tag); //добавляем в контекст
                        }

                        photo.Tags.Add(tag); //привязываем тег к фотографии
                    }
                }

                //СОХРАНЕНИЕ В БАЗЕ ДАННЫХ
                dbContext.Photos.Add(photo); //добавляем фото в контекст
                dbContext.SaveChanges(); //сохраняем изменения в БД

                MessageBox.Show($"Фотография '{TitleTextBox.Text.Trim()}' успешно добавлена!",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                this.DialogResult = true; //говорим главному окну что сохранение успешно
                this.Close(); //закрываем окно
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //кнопка отмена
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; //сообщает что пользователь нажал отмену
            this.Close(); //закрываем окно
        }

        //событие закрытия окна
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            dbContext?.Dispose(); //освобождаем контекст БД
        }
    }
}