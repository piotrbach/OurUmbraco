var gulp         = require('gulp'),
    gutil        = require('gulp-util'),
    sass         = require('gulp-sass'),
    jshint       = require('gulp-jshint'),
    stylish      = require('jshint-stylish'),
    uglify       = require('gulp-uglify'),
    concat       = require('gulp-concat'),
    autoprefixer = require('gulp-autoprefixer'),
    minifyCss    = require('gulp-minify-css'),
    imageMin     = require('gulp-imagemin'),
    clean        = require('gulp-clean'),
    svgmin       = require('gulp-svgmin'),
    mmq          = require('gulp-merge-media-queries'),
	rename       = require('gulp-rename');

var settings = {
  build: './build',
  source: 'src',
  umbraco: './../OurUmbraco.Site'
};

// Lint js files
gulp.task('lint', function() {
  'use strict';

  return gulp.src(settings.source+'/js/*.js')
  .pipe(jshint())
  .pipe(jshint.reporter(stylish));
});

// Minify js files
gulp.task('js', function() {
  'use strict';

  return gulp.src([settings.source+'/js/vendor/**/*.js', settings.source+'/js/*.js'])

  // Mangle is set to false by default.
  // By disabling mangle, Uglify won't rename
  // variables, functions etc.
  // This means that AngularJS apps will be
  // able to be minified without breaking.
  // If you aren't using angular, I recommend
  // enabling mangle, to obtain better compression.
  .pipe(uglify({mangle: false}))

  .pipe(concat('app.min.js'))
  .on('error', gutil.log)
  .pipe(gulp.dest(settings.build+'/assets/js'))
  .pipe(gulp.dest(settings.umbraco+'/assets/js'));
});

[settings.source + '/css/**/*.css', settings.source + '/scss/style.scss'];

// Compile vendor css and scss
gulp.task('css', function () {
    return gulp.src(settings.source + '/scss/*.scss')
        .pipe(sass())
		.on('error', gutil.log)
		.pipe(autoprefixer('last 1 version', 'ie 9', 'ios 7'))
		.pipe(mmq({log: false}))
		.pipe(rename({suffix: '.min'}))
		.pipe(minifyCss())
        .pipe(gulp.dest(settings.build + '/assets/css'))
		.pipe(gulp.dest(settings.umbraco + '/assets/css'));
});

// Optimize images
gulp.task('images', function(){
  'use strict';

    return gulp.src([settings.source + '/images/*.png', settings.source + '/images/*.jpg', settings.source + '/images/*.gif'])
    .pipe(imageMin())
    .on('error', gutil.log)
    .pipe(gulp.dest(settings.build + '/assets/images'))
    .pipe(gulp.dest(settings.umbraco + '/assets/images'));
});

// SVG images
gulp.task('svg', function() {
  'use strict';

  return gulp.src(settings.source+'/images/*.svg')
  .pipe(svgmin())
  .on('error', gutil.log)
  .pipe(gulp.dest(settings.build+'/assets/images'))
  .pipe(gulp.dest(settings.umbraco+'/assets/images'));
});

// Build task to used during normal development where you don't need the images to be updated.
gulp.task('dev', gulp.parallel(['lint', 'js', 'css']));

// The same as above, but also minifying all the image files.
gulp.task('build', gulp.parallel(['lint', 'js', 'css', 'images', 'svg']));

// Default task and watch files
gulp.task('default', gulp.series('build', function() {
  'use strict';

  gulp.watch([settings.source+'/js/vendor/**/*.js', settings.source+'/js/*.js'], ['lint', 'js']);
  gulp.watch([settings.source+'/scss/**/*.scss', settings.source+'/css/**/*.css'], ['css']);
  gulp.watch(settings.source+'/images/**', ['images', 'svg']);
}));
