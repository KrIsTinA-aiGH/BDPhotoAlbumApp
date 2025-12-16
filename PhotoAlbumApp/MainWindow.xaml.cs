using PhotoAlbumApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PhotoAlbumApp
{
    public partial class MainWindow : Window
    {
        private PhotoAlbumDBEntities dbContext;//контекст базы данных

        //главное окно приложения
        public MainWindow()
        {
            InitializeComponent();
            dbContext = new PhotoAlbumDBEntities(); //создаем контекст бд
            LoadData(); //загружаем данные для отображения
        }


        //загрузка данных при записи
        private void LoadData()
        {
            try
            {
                //загрузка категорий
                //получаем все категории из таблицы,весь результат сохраняется в список allCategoriesFromDb
                var allCategoriesFromDb = dbContext.Categories.ToList();

                //создаем список
                var categoriesList = new List<Categories>();
                categoriesList = allCategoriesFromDb; //копируем все категории

                //заполняем список категорий к выпадающему списку
                CategoryComboBox.ItemsSource = categoriesList;
                CategoryComboBox.SelectedIndex = 0; //выбираем "Все категории" по умолчанию

                //загрузка фотографий
                LoadPhotos();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //загрузка фотографий
        private void LoadPhotos()
        {
            try
            {
                //получаем выбранную категорию
                var selectedCategory = CategoryComboBox.SelectedItem as Categories;

                //начинаем запрс к бд
                var query = dbContext.Photos.AsQueryable();

                //ФИЛЬТРАЦИЯ ПО КАТЕГОРИИ
                if (selectedCategory != null && selectedCategory.Name != "Все категории")
                {
                    query = query.Where(p => p.CategoryId == selectedCategory.CategoryId);
                }

                //ФИЛЬТРАЦИЯ ПО ПОИСКОВОМУ ЗАПРОСУ
                if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    string searchText = SearchTextBox.Text.ToLower(); //приводим к нижнему регистру
                    //ищем в названии, описании и тегах
                    query = query.Where(p =>
                        p.Title.ToLower().Contains(searchText) ||
                        p.Description.ToLower().Contains(searchText) ||
                        p.Tags.Any(t => t.Name.ToLower().Contains(searchText))
                    );
                }

                //ВЫПОЛНЯЕМ ЗАПРОС И ВЫВОДИМ В ListBox
                PhotosListBox.ItemsSource = query.ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фото: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //--- ОБРАБОТЧИКИ СОБЫТИЙ ---

        //изменение выбранной категории в ComboBox
        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadPhotos(); //перезагружаем фото
        }

        //изменение текста в поле поиска
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadPhotos(); //перезагружаем фото с новым поисковым запросом
        }

        //выбор фотографии в ListBox
        private void PhotosListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedPhoto = PhotosListBox.SelectedItem as Photos; //получаем выбранную фотографию 

            //если ничего не выбрано скрываем панель деталей
            if (selectedPhoto == null)
            {
                PhotoDetailsPanel.Visibility = Visibility.Collapsed; //скрываем и выходим
                return;
            }

            //показывает панель
            PhotoDetailsPanel.Visibility = Visibility.Visible;

            //ЗАПОЛНЯЕМ ИНФОРМАЦИЮ О ФОТО
            PhotoTitleText.Text = selectedPhoto.Title; //название
            DescriptionText.Text = selectedPhoto.Description ?? "(без описания)"; //описание
            CategoryText.Text = selectedPhoto.Categories?.Name ?? "(без категории)"; //категория

            //теги-объединяем через запятую или показываем "(без тегов)"
            TagsText.Text = selectedPhoto.Tags.Any()
                ? string.Join(", ", selectedPhoto.Tags.Select(t => t.Name))
                : "(без тегов)";

            CreatedDateText.Text = selectedPhoto.CreatedDate?.ToString("dd.MM.yyyy HH:mm") ?? "(дата не указана)";

            //ЗАГРУЗКА ИЗОБРАЖЕНИЯ
            try
            {
                //пытаемся загрузить изображение из файла(есть ли путь к файлу И существует ли файл на диске)
                if (!string.IsNullOrEmpty(selectedPhoto.FilePath) &&
                    System.IO.File.Exists(selectedPhoto.FilePath))
                {
                    var bitmap = new BitmapImage(); //создает объект изображения
                    bitmap.BeginInit(); //начинаем настройку
                    bitmap.UriSource = new Uri(selectedPhoto.FilePath); // указывает путь к файлу
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; //кэшируем при загрузке
                    bitmap.EndInit(); //завершаем инициализацию
                    PhotoImage.Source = bitmap; //устанавливаем как Image
                }
                else
                {
                    PhotoImage.Source = new BitmapImage(
                            new Uri("pack://application:,,,/Images/default.png"));
                }
            }
            catch (Exception)
            {
                PhotoImage.Source = null; // При любой ошибке очищаем
            }
        }

        //кнопка добавить
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //создаем окно добавления фото
                var addWindow = new AddEditPhotoWindow();
                addWindow.Owner = this; //устанавливаем текущее окно как владельца
                addWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner; //центрируем

                //показываем как модальное диалоговое окно
                if (addWindow.ShowDialog() == true) //если пользователь нажал "Сохранить"
                {
                    LoadPhotos(); //обновляем список фотографий
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна добавления: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //кнопка удалить
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPhoto = PhotosListBox.SelectedItem as Photos;

            //проверяем что фото выбрано
            if (selectedPhoto == null)
            {
                MessageBox.Show("Выберите фотографию для удаления",
                    "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            //ЗАПРАШИВАЕМ ПОДТВЕРЖДЕНИЕ
            var result = MessageBox.Show($"Удалить фотографию '{selectedPhoto.Title}'?",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes) //если пользователь подтвердил
            {
                try
                {
                    //получаем актуальные данные из базы, включая теги
                    var photoWithTags = dbContext.Photos
                        .Include("Tags") //загружаем теги
                        .FirstOrDefault(p => p.PhotoId == selectedPhoto.PhotoId);

                    if (photoWithTags != null)
                    {
                        //удаляем теги, которые больше не используются другими фотографиями
                        //сначала проверить каждый тег
                        foreach (var tag in photoWithTags.Tags.ToList())
                        {
                            //проверяем используется ли тег другими фотографиями
                            bool isTagUsed = dbContext.Photos
                                .Any(p => p.PhotoId != photoWithTags.PhotoId &&
                                         p.Tags.Any(t => t.TagId == tag.TagId)); //будет возвращать истину если хотя бы одна отография будет иметь этот тэе

                            if (!isTagUsed)
                            {
                                //если тег больше нигде не используется - удаляем из бд
                                dbContext.Tags.Remove(tag);
                            }
                        }

                        //очищаем коллекцию тегов у фотографии
                        photoWithTags.Tags.Clear();

                        //удаляем фото из бд
                        dbContext.Photos.Remove(photoWithTags);
                        dbContext.SaveChanges(); //сохраняем все изменения

                        //обновляем интерфейс
                        LoadPhotos(); //обновляем список
                        PhotoDetailsPanel.Visibility = Visibility.Collapsed; //скрываем панель деталей

                        MessageBox.Show("Фотография и связанные данные удалены",
                            "Удаление", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}